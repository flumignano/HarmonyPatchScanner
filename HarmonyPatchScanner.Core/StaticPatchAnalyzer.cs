using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyPatchScanner.Core
{
    public sealed class StaticPatchAnalyzer
    {
        // Reflection exposes IL as raw bytes; these lookup tables let us decode only enough
        // instruction shape to recognize conservative, non-executing safety findings.
        private static readonly OpCode[] SingleByteOpCodes = new OpCode[0x100];
        private static readonly OpCode[] MultiByteOpCodes = new OpCode[0x100];

        static StaticPatchAnalyzer()
        {
            foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.GetValue(null) is not OpCode opCode)
                    continue;

                var value = unchecked((ushort)opCode.Value);
                if (value < 0x100)
                    SingleByteOpCodes[value] = opCode;
                else if ((value & 0xff00) == 0xfe00)
                    MultiByteOpCodes[value & 0xff] = opCode;
            }
        }

        public IReadOnlyList<StaticPatchFinding> Analyze(PatchScanSnapshot snapshot)
        {
            var factsByPatch = snapshot.Patches.ToDictionary(patch => patch, InspectPatch);
            var findings = new List<StaticPatchFinding>();

            foreach (var targetGroup in snapshot.Patches.GroupBy(patch => patch.TargetMethod))
            {
                var targetFacts = targetGroup
                    .Select(patch => factsByPatch[patch])
                    .ToList();

                var resultWriters = targetFacts
                    .Where(facts => facts.WritesResult)
                    .ToList();

                var refWritersByName = targetFacts
                    .SelectMany(facts => facts.RefArgumentWrites.Select(name => new { Facts = facts, Name = name }))
                    .GroupBy(entry => entry.Name, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Select(entry => entry.Facts).ToList(), StringComparer.Ordinal);

                foreach (var facts in targetFacts)
                {
                    var patchFindings = BuildFindings(facts, resultWriters, refWritersByName);
                    facts.Patch.StaticFindings = patchFindings;
                    findings.AddRange(patchFindings);
                }
            }

            return findings;
        }

        private static PatchStaticFacts InspectPatch(PatchRecord patch)
        {
            var facts = new PatchStaticFacts(patch);
            var method = patch.PatchMethodBase;

            if (method == null)
            {
                facts.MarkUnreadable("Patch method metadata is unavailable; static IL inspection skipped.");
                return facts;
            }

            byte[] ilBytes;
            try
            {
                var body = method.GetMethodBody();
                ilBytes = body?.GetILAsByteArray() ?? Array.Empty<byte>();
            }
            catch (Exception ex)
            {
                facts.MarkUnreadable("Patch method body could not be read: " + ex.GetType().Name + ".");
                return facts;
            }

            if (ilBytes.Length == 0)
            {
                facts.MarkUnreadable("Patch method has no readable IL body.");
                return facts;
            }

            IReadOnlyList<IlInstruction> instructions;
            try
            {
                instructions = Decode(ilBytes);
            }
            catch (Exception ex)
            {
                facts.MarkUnreadable("Patch method IL could not be decoded: " + ex.GetType().Name + ".");
                return facts;
            }

            // This is the only deterministic v1 claim: a simple bool prefix body that always
            // returns false will make Harmony skip the original method whenever it runs.
            if (patch.PatchType == HarmonyPatchKind.Prefix &&
                method is MethodInfo methodInfo &&
                methodInfo.ReturnType == typeof(bool) &&
                IsUnconditionalFalseReturn(instructions))
            {
                facts.UnconditionallySkipsOriginal = true;
            }

            foreach (var parameter in method.GetParameters())
            {
                var name = parameter.Name ?? string.Empty;
                var ilArgumentIndex = method.IsStatic ? parameter.Position : parameter.Position + 1;

                // A by-ref __result write is common and useful; it becomes "likely" only
                // when another result writer targets the same method.
                if (string.Equals(name, "__result", StringComparison.Ordinal) &&
                    parameter.ParameterType.IsByRef &&
                    WritesThroughArgument(instructions, ilArgumentIndex))
                {
                    facts.WritesResult = true;
                }
                else if (name.StartsWith("___", StringComparison.Ordinal))
                {
                    // Triple-underscore parameters are Harmony's private-field access convention.
                    facts.PrivateFieldAccesses.Add(name);
                }
                else if (parameter.ParameterType.IsByRef &&
                         !name.StartsWith("__", StringComparison.Ordinal) &&
                         WritesThroughArgument(instructions, ilArgumentIndex))
                {
                    // Ref/out original arguments are visible side effects on the target call.
                    facts.RefArgumentWrites.Add(name);
                }
            }

            return facts;
        }

        private static IReadOnlyList<StaticPatchFinding> BuildFindings(
            PatchStaticFacts facts,
            IReadOnlyList<PatchStaticFacts> resultWriters,
            IReadOnlyDictionary<string, List<PatchStaticFacts>> refWritersByName)
        {
            var findings = new List<StaticPatchFinding>();
            var patch = facts.Patch;

            if (facts.UnreadableReason != null)
            {
                findings.Add(CreateFinding(
                    patch,
                    StaticFindingConfidence.Potential,
                    StaticFindingKind.UnreadableBody,
                    facts.UnreadableReason));
            }

            if (facts.UnconditionallySkipsOriginal)
            {
                findings.Add(CreateFinding(
                    patch,
                    StaticFindingConfidence.Deterministic,
                    StaticFindingKind.UnconditionalSkipOriginal,
                    "Prefix appears to return false unconditionally, so Harmony will skip the original method."));
            }

            if (facts.WritesResult)
            {
                var confidence = resultWriters.Count > 1
                    ? StaticFindingConfidence.Likely
                    : StaticFindingConfidence.Potential;

                var explanation = resultWriters.Count > 1
                    ? "Patch writes through ref/out __result on a target with " + resultWriters.Count + " result writers; later writers may replace earlier values."
                    : "Patch writes through ref/out __result. This is common and not a conflict by itself.";

                findings.Add(CreateFinding(
                    patch,
                    confidence,
                    StaticFindingKind.ResultWrite,
                    explanation));
            }

            foreach (var argumentName in facts.RefArgumentWrites.Distinct(StringComparer.Ordinal))
            {
                refWritersByName.TryGetValue(argumentName, out var writers);
                var writerCount = writers?.Count ?? 1;
                var confidence = writerCount > 1
                    ? StaticFindingConfidence.Likely
                    : StaticFindingConfidence.Potential;

                var explanation = writerCount > 1
                    ? "Patch writes through ref/out argument '" + argumentName + "' on a target with " + writerCount + " writers for that argument."
                    : "Patch writes through ref/out argument '" + argumentName + "'. This is a visible side effect, but not a conflict by itself.";

                findings.Add(CreateFinding(
                    patch,
                    confidence,
                    StaticFindingKind.RefArgumentMutation,
                    explanation));
            }

            if (facts.PrivateFieldAccesses.Count > 0)
            {
                findings.Add(CreateFinding(
                    patch,
                    StaticFindingConfidence.Potential,
                    StaticFindingKind.PrivateFieldAccess,
                    "Patch accesses private field parameter(s) via Harmony's triple-underscore convention: " +
                    string.Join(", ", facts.PrivateFieldAccesses.Distinct(StringComparer.Ordinal)) + "."));
            }

            return findings;
        }

        private static StaticPatchFinding CreateFinding(
            PatchRecord patch,
            StaticFindingConfidence confidence,
            StaticFindingKind kind,
            string explanation)
        {
            return new StaticPatchFinding(
                patch.TargetMethod,
                patch.PatchMethod,
                patch.Owner,
                patch.PatchType,
                patch.Index,
                confidence,
                kind,
                explanation);
        }

        private static bool IsUnconditionalFalseReturn(IReadOnlyList<IlInstruction> instructions)
        {
            // Keep this intentionally narrow: avoid calling conditional paths deterministic.
            var meaningful = instructions
                .Where(instruction => instruction.OpCode != OpCodes.Nop)
                .ToList();

            if (meaningful.Count < 2 || meaningful.Any(IsConditionalBranch))
                return false;

            var last = meaningful[meaningful.Count - 1];
            if (last.OpCode != OpCodes.Ret)
                return false;

            var previous = meaningful[meaningful.Count - 2];
            if (IsLoadConstantZero(previous))
                return true;

            var localIndex = GetLoadLocalIndex(previous);
            if (localIndex == null)
                return false;

            for (var i = meaningful.Count - 3; i >= 1; i--)
            {
                if (GetStoreLocalIndex(meaningful[i]) == localIndex &&
                    IsLoadConstantZero(meaningful[i - 1]))
                {
                    return true;
                }

                if (meaningful[i].OpCode.FlowControl == FlowControl.Call ||
                    meaningful[i].OpCode.FlowControl == FlowControl.Throw)
                {
                    return false;
                }
            }

            return false;
        }

        private static bool WritesThroughArgument(IReadOnlyList<IlInstruction> instructions, int argumentIndex)
        {
            // We only claim a write when a store-through-pointer opcode is near a load of that argument.
            // This misses clever flows by design, but prevents scary false certainty.
            for (var i = 0; i < instructions.Count; i++)
            {
                if (!IsIndirectWrite(instructions[i].OpCode))
                    continue;

                var start = Math.Max(0, i - 5);
                for (var j = i - 1; j >= start; j--)
                {
                    if (GetLoadArgumentIndex(instructions[j]) == argumentIndex)
                        return true;
                }
            }

            return false;
        }

        private static bool IsIndirectWrite(OpCode opCode)
        {
            return opCode == OpCodes.Stind_I ||
                   opCode == OpCodes.Stind_I1 ||
                   opCode == OpCodes.Stind_I2 ||
                   opCode == OpCodes.Stind_I4 ||
                   opCode == OpCodes.Stind_I8 ||
                   opCode == OpCodes.Stind_R4 ||
                   opCode == OpCodes.Stind_R8 ||
                   opCode == OpCodes.Stind_Ref ||
                   opCode == OpCodes.Stobj ||
                   opCode == OpCodes.Initobj ||
                   opCode == OpCodes.Cpobj;
        }

        private static bool IsConditionalBranch(IlInstruction instruction)
        {
            return instruction.OpCode.FlowControl == FlowControl.Cond_Branch;
        }

        private static bool IsLoadConstantZero(IlInstruction instruction)
        {
            if (instruction.OpCode == OpCodes.Ldc_I4_0)
                return true;

            if ((instruction.OpCode == OpCodes.Ldc_I4 ||
                 instruction.OpCode == OpCodes.Ldc_I4_S) &&
                instruction.Operand is int value)
            {
                return value == 0;
            }

            return false;
        }

        private static int? GetLoadArgumentIndex(IlInstruction instruction)
        {
            if (instruction.OpCode == OpCodes.Ldarg_0)
                return 0;
            if (instruction.OpCode == OpCodes.Ldarg_1)
                return 1;
            if (instruction.OpCode == OpCodes.Ldarg_2)
                return 2;
            if (instruction.OpCode == OpCodes.Ldarg_3)
                return 3;
            if ((instruction.OpCode == OpCodes.Ldarg ||
                 instruction.OpCode == OpCodes.Ldarg_S ||
                 instruction.OpCode == OpCodes.Ldarga ||
                 instruction.OpCode == OpCodes.Ldarga_S) &&
                instruction.Operand is int value)
            {
                return value;
            }

            return null;
        }

        private static int? GetLoadLocalIndex(IlInstruction instruction)
        {
            if (instruction.OpCode == OpCodes.Ldloc_0)
                return 0;
            if (instruction.OpCode == OpCodes.Ldloc_1)
                return 1;
            if (instruction.OpCode == OpCodes.Ldloc_2)
                return 2;
            if (instruction.OpCode == OpCodes.Ldloc_3)
                return 3;
            if ((instruction.OpCode == OpCodes.Ldloc ||
                 instruction.OpCode == OpCodes.Ldloc_S ||
                 instruction.OpCode == OpCodes.Ldloca ||
                 instruction.OpCode == OpCodes.Ldloca_S) &&
                instruction.Operand is int value)
            {
                return value;
            }

            return null;
        }

        private static int? GetStoreLocalIndex(IlInstruction instruction)
        {
            if (instruction.OpCode == OpCodes.Stloc_0)
                return 0;
            if (instruction.OpCode == OpCodes.Stloc_1)
                return 1;
            if (instruction.OpCode == OpCodes.Stloc_2)
                return 2;
            if (instruction.OpCode == OpCodes.Stloc_3)
                return 3;
            if ((instruction.OpCode == OpCodes.Stloc ||
                 instruction.OpCode == OpCodes.Stloc_S) &&
                instruction.Operand is int value)
            {
                return value;
            }

            return null;
        }

        private static IReadOnlyList<IlInstruction> Decode(byte[] il)
        {
            var instructions = new List<IlInstruction>();
            var offset = 0;

            while (offset < il.Length)
            {
                var instructionOffset = offset;
                var code = il[offset++];
                OpCode opCode;

                if (code == 0xfe)
                {
                    if (offset >= il.Length)
                        throw new InvalidOperationException("Incomplete multi-byte opcode.");

                    opCode = MultiByteOpCodes[il[offset++]];
                }
                else
                {
                    opCode = SingleByteOpCodes[code];
                }

                if (opCode.Size == 0)
                    throw new InvalidOperationException("Unknown opcode.");

                var operand = ReadOperand(il, ref offset, opCode.OperandType);
                instructions.Add(new IlInstruction(instructionOffset, opCode, operand));
            }

            return instructions;
        }

        private static object? ReadOperand(byte[] il, ref int offset, OperandType operandType)
        {
            switch (operandType)
            {
                case OperandType.InlineNone:
                    return null;

                case OperandType.ShortInlineI:
                    RequireBytes(il, offset, 1);
                    return (int)(sbyte)il[offset++];

                case OperandType.InlineI:
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                    RequireBytes(il, offset, 4);
                    var intValue = BitConverter.ToInt32(il, offset);
                    offset += 4;
                    return intValue;

                case OperandType.InlineI8:
                    RequireBytes(il, offset, 8);
                    var longValue = BitConverter.ToInt64(il, offset);
                    offset += 8;
                    return longValue;

                case OperandType.ShortInlineR:
                    RequireBytes(il, offset, 4);
                    var floatValue = BitConverter.ToSingle(il, offset);
                    offset += 4;
                    return floatValue;

                case OperandType.InlineR:
                    RequireBytes(il, offset, 8);
                    var doubleValue = BitConverter.ToDouble(il, offset);
                    offset += 8;
                    return doubleValue;

                case OperandType.ShortInlineBrTarget:
                    RequireBytes(il, offset, 1);
                    return (int)(sbyte)il[offset++];

                case OperandType.ShortInlineVar:
                    RequireBytes(il, offset, 1);
                    return (int)il[offset++];

                case OperandType.InlineVar:
                    RequireBytes(il, offset, 2);
                    var ushortValue = BitConverter.ToUInt16(il, offset);
                    offset += 2;
                    return (int)ushortValue;

                case OperandType.InlineSwitch:
                    RequireBytes(il, offset, 4);
                    var count = BitConverter.ToInt32(il, offset);
                    offset += 4;
                    RequireBytes(il, offset, count * 4);
                    offset += count * 4;
                    return count;

                default:
                    throw new NotSupportedException("Unsupported operand type: " + operandType);
            }
        }

        private static void RequireBytes(byte[] il, int offset, int count)
        {
            if (offset + count > il.Length)
                throw new InvalidOperationException("Incomplete IL operand.");
        }

        private sealed class PatchStaticFacts
        {
            public PatchStaticFacts(PatchRecord patch)
            {
                Patch = patch;
            }

            public PatchRecord Patch { get; }

            public string? UnreadableReason { get; private set; }

            public bool UnconditionallySkipsOriginal { get; set; }

            public bool WritesResult { get; set; }

            public List<string> RefArgumentWrites { get; } = new List<string>();

            public List<string> PrivateFieldAccesses { get; } = new List<string>();

            public void MarkUnreadable(string reason)
            {
                UnreadableReason = reason;
            }
        }

        private sealed class IlInstruction
        {
            public IlInstruction(int offset, OpCode opCode, object? operand)
            {
                Offset = offset;
                OpCode = opCode;
                Operand = operand;
            }

            public int Offset { get; }

            public OpCode OpCode { get; }

            public object? Operand { get; }
        }
    }
}

# RimWorld Adapter Template

`RimWorldPatchScannerAdapter.cs` is a drop-in starting point for the RimWorld mod.
It is kept outside the core project because it references Verse/RimWorld APIs that
are not available in this Bannerlord source folder.

Expected integration:

```csharp
var options = PatchScannerOptions.CreateRimWorldDefaults();
var service = new PatchScannerService(new RimWorldPatchScannerAdapter());

service.ExportAllPatches(options);
service.ExportConflictReport(options);
service.ExportModuleReport(options, "your.package.id");
```

Wire those calls to a mod settings button, a DevMode gizmo, or any debug action you
prefer. The service writes the same report file names used by the Bannerlord tool.

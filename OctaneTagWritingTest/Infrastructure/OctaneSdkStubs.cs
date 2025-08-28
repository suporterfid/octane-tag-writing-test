namespace Impinj.OctaneSdk;

// Stub delegates for build environments lacking full Octane SDK event handler definitions.
public delegate void TagsReportedEventHandler(ImpinjReader reader, TagReport report);
public delegate void TagOpCompleteEventHandler(ImpinjReader reader, TagOpReport report);
public delegate void GpiEventHandler(ImpinjReader reader, GpiEvent gpiEvent);

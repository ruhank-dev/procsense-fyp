namespace ProcSense.VR.Boot
{
    /// <summary>
    /// Which build this is (p0-vr-platform-s07), from the scripting define the build script sets: Dev (a development build with the profiler attachable and the developer menus), Demo (a release build for showing the
    /// simulator) or Study (a release build for the evaluation sessions). The Editor and an unmarked build are Dev. The session log header and the boot screen can show it, so a capture or a session file always says
    /// which build made it.
    /// </summary>
    public static class BuildVariant
    {
        public const string Dev = "dev", Demo = "demo", Study = "study";

#if PROCSENSE_STUDY
        public const string Name = Study;
#elif PROCSENSE_DEMO
        public const string Name = Demo;
#else
        public const string Name = Dev;
#endif

        public static bool IsRelease => Name != Dev;
    }
}

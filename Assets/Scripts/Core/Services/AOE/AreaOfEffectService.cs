public static class AreaOfEffectService
{
    public static IAreaOfEffectService Instance { get; private set; }

    public static void Register(IAreaOfEffectService service)
    {
        Instance = service;
    }
}
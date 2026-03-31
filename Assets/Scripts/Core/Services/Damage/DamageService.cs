public static class DamageService
{
    public static IDamageService Instance { get; private set; }

    public static void Register(IDamageService service)
    {
        Instance = service;
    }
}
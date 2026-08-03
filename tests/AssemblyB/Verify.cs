using Everlong.DI;

// Verify SG generated code in AssemblyB
public static class Verify
{
    public static bool Run()
    {
        var svc = new ServiceB();
        // Inject won't be called since we don't have a service provider,
        // but just verifying the code compiles proves the SG works.
        return svc is IInjectable;
    }
}

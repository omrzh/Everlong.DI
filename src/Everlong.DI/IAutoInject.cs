namespace Everlong.DI;

/// <summary>
///   Marks a class as **auto-injection managed**: an <c>Inject(IServiceProvider)</c>
///   implementation is generated for it (and, through override chaining, for every
///   derived class that declares <see cref="InjectAttribute"/> members).
/// </summary>
/// <remarks>
///   <para>
///     This is the generator anchor. The generated partial for a chain-starting class
///     declares this interface (or the class may declare it in source — e.g. a
///     framework base class that owns no <see cref="InjectAttribute"/> members of its
///     own still opts its hierarchy in by implementing <see cref="IAutoInject"/>).
///   </para>
///   <para>
///     Derived classes do <b>not</b> need to repeat the marker: a partial class that
///     merely declares <see cref="InjectAttribute"/> members is a generation target on
///     its own. Every generated type that implements this interface also implements
///     <see cref="IInjectable"/>, which is the resolution contract the
///     <see cref="IInjector"/> / <see cref="IInjectorServiceProvider"/> wrappers check.
///   </para>
/// </remarks>
public interface IAutoInject : IInjectable
{
}

namespace Everlong.DI.Dogfood;

// ── Core service contracts shared by the member-injection and registration demos ──

public interface IShell { string Tag { get; } }
public interface IRouter { string Tag { get; } }
public interface IPageService { string Tag { get; } }
public interface IClock { string Tag { get; } }
public interface ICache { string Tag { get; } }
public interface IRemoteConfig { }                 // deliberately never registered

public enum CacheTier { Fast, Slow }

public readonly record struct PostDetailArgs(int Id);

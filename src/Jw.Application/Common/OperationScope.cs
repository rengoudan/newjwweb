using Jw.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Jw.Application.Common;

/// <summary>
/// 为一次操作新开一个 DI 作用域，并把当前操作者带进去。
/// 用于 UserManager、RoleManager 这类依赖作用域内 DbContext 的服务：
/// 在 Blazor 交互连接里直接注入它们，DbContext 会活到连接结束，数据容易过期。
/// </summary>
public sealed class OperationScope(IServiceScopeFactory scopeFactory, CurrentUserState state)
{
    public async Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentUserState>().CopyFrom(state);
        return await work(scope.ServiceProvider);
    }

    public Task RunAsync(Func<IServiceProvider, Task> work) =>
        RunAsync<bool>(async sp => { await work(sp); return true; });
}

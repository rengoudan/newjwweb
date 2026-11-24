using Jw.Application.Common;
using MudBlazor;

namespace Jw.Web.Shared;

/// <summary>页面执行操作的统一包装：业务异常提示给用户，其他异常记日志并给通用提示。</summary>
public sealed class UiRunner(ISnackbar snackbar, ILogger<UiRunner> logger)
{
    public async Task<bool> RunAsync(Func<Task> action, string? successMessage = null)
    {
        try
        {
            await action();
            if (successMessage is not null) snackbar.Add(successMessage, Severity.Success);
            return true;
        }
        catch (AppException ex)
        {
            snackbar.Add(ex.Message, ex is ConcurrencyConflictException ? Severity.Warning : Severity.Error);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "画面操作でエラー");
            snackbar.Add("予期しないエラーが発生しました。しばらくしてから再度お試しください。", Severity.Error);
            return false;
        }
    }
}

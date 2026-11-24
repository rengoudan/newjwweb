namespace Jw.Application.Common;

/// <summary>可以直接把 Message 显示给用户的业务异常。</summary>
public class AppException(string message) : Exception(message);

public sealed class ForbiddenException(string permission)
    : AppException("この操作を行う権限がありません。")
{
    public string Permission { get; } = permission;
}

public sealed class NotFoundException(string what)
    : AppException($"{what}が見つかりません。削除された可能性があります。");

public sealed class ConcurrencyConflictException()
    : AppException("他のユーザーがこのデータを変更しました。画面を再読み込みしてからやり直してください。");

using Microsoft.AspNetCore.Identity;

namespace Jw.Web.Auth;

/// <summary>Identity 的错误信息改为日语（用户会直接看到的部分）。</summary>
public sealed class JapaneseIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError PasswordMismatch() => new() { Code = nameof(PasswordMismatch), Description = "現在のパスワードが正しくありません。" };
    public override IdentityError PasswordTooShort(int length) => new() { Code = nameof(PasswordTooShort), Description = $"パスワードは{length}文字以上にしてください。" };
    public override IdentityError PasswordRequiresDigit() => new() { Code = nameof(PasswordRequiresDigit), Description = "パスワードには数字を1文字以上含めてください。" };
    public override IdentityError PasswordRequiresLower() => new() { Code = nameof(PasswordRequiresLower), Description = "パスワードには英小文字を1文字以上含めてください。" };
    public override IdentityError PasswordRequiresUpper() => new() { Code = nameof(PasswordRequiresUpper), Description = "パスワードには英大文字を1文字以上含めてください。" };
    public override IdentityError PasswordRequiresNonAlphanumeric() => new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "パスワードには記号を1文字以上含めてください。" };
    public override IdentityError DuplicateUserName(string userName) => new() { Code = nameof(DuplicateUserName), Description = $"ユーザーID「{userName}」は既に使われています。" };
    public override IdentityError InvalidUserName(string? userName) => new() { Code = nameof(InvalidUserName), Description = $"ユーザーID「{userName}」は使用できません。" };
    public override IdentityError DuplicateRoleName(string role) => new() { Code = nameof(DuplicateRoleName), Description = $"ロールID「{role}」は既に使われています。" };
    public override IdentityError ConcurrencyFailure() => new() { Code = nameof(ConcurrencyFailure), Description = "他のユーザーが同時に変更しました。再読み込みしてやり直してください。" };
}

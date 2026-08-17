namespace InfinitoCoffee.Application.Authentication.Contracts;

public enum UserPasswordVerificationResult
{
    Failed = 0,
    Success = 1,
    SuccessRehashNeeded = 2
}

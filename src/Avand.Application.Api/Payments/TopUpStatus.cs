using Avand.Application.Api.Persistence;

namespace Avand.Application.Api.Payments;

internal static class TopUpStatus
{
    internal static object Response(TopUp topUp) => new
    {
        topUpId = topUp.Id,
        topUp.AmountIrr,
        status = topUp.PostingState ?? topUp.ProviderState,
        topUp.MessageCode,
        message = topUp.MessageCode switch
        {
            "FinancialEligibilityRequired" => "این حساب در حال حاضر مجاز به شروع شارژ نیست.",
            "AwaitingLedger" => "پرداخت تأیید شده و ثبت وجه در کیف پول در انتظار تأیید است.",
            "VerifyExpiredIssuerReturnWithin72Hours" =>
                "تأیید پرداخت منقضی شد. در این سناریو، بانک صادرکننده وجه برداشت‌شده را ظرف ۷۲ ساعت به کارت بازمی‌گرداند.",
            "ProviderRejected" => "درگاه پرداخت را رد کرده است.",
            "IssuerReturnConfirmed" => "درگاه بازگشت وجه به کارت را تأیید کرده است.",
            "CreditRestricted" => "وجه در ماندهٔ محدود ثبت شده و قابل مصرف نیست.",
            "WalletCredited" => "وجه به موجودی قابل‌مصرف کیف پول اضافه شد.",
            "PostingRequiresReview" => "پرداخت تأیید شده و ثبت آن نیاز به بررسی دارد.",
            _ => "نتیجهٔ پرداخت در حال پیگیری است."
        },
        continuation = topUp.ProviderState == "AwaitingProvider" ? topUp.Continuation : null,
        topUp.CreditedBucket
    };
}

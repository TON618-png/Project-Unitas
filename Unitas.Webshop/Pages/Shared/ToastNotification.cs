using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Unitas.Webshop.Pages;

internal static class ToastNotification
{
    public const string MessageKey = "ToastMessage";
    public const string ErrorKey = "ToastError";

    public static void ShowToast(this ITempDataDictionary tempData, string message, bool isError = false)
    {
        tempData[MessageKey] = message;
        tempData[ErrorKey] = isError;
    }
}

using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace FPolyCinema.Web.Infrastructure.Web;

/// <summary>Một toast gửi qua lần chuyển trang (TempData), layout hiện bằng ui.toast.</summary>
public sealed record FlashMessage(string Type, string Message);

/// <summary>Đọc, ghi toast chờ hiện sau khi chuyển trang (ví dụ "Đã lưu thay đổi" sau khi Lưu form).</summary>
public static class Flash
{
    private const string Key = "fpc.flash";

    public static void Add(ITempDataDictionary tempData, string type, string message)
    {
        var list = Peek(tempData);
        list.Add(new FlashMessage(type, message));
        tempData[Key] = JsonSerializer.Serialize(list);
    }

    public static List<FlashMessage> Read(ITempDataDictionary tempData)
    {
        var list = Peek(tempData);
        tempData.Remove(Key);
        return list;
    }

    private static List<FlashMessage> Peek(ITempDataDictionary tempData)
    {
        if (tempData.Peek(Key) is not string raw || raw.Length == 0) return new List<FlashMessage>();
        try { return JsonSerializer.Deserialize<List<FlashMessage>>(raw) ?? new List<FlashMessage>(); }
        catch (JsonException) { return new List<FlashMessage>(); }
    }
}

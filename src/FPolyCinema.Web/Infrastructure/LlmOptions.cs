namespace FPolyCinema.Web.Infrastructure;

/// <summary>
/// Khóa Llm (CONTRACTS C7-07) cho LlmAgent của trợ lý đặt vé (SPEC 4.2-02): một client theo chuẩn OpenAI Chat Completions có function calling,
/// dùng được với OpenAI, Gemini (endpoint tương thích OpenAI) hoặc máy chủ cục bộ như Ollama. ApiKey chỉ đặt qua biến môi trường Llm__ApiKey;
/// để trống thì trợ lý chạy LocalAgent (không cần mạng).
/// </summary>
public class LlmOptions
{
    public const string SectionName = "Llm";

    /// <summary>Gốc API, ví dụ https://api.openai.com/v1, https://generativelanguage.googleapis.com/v1beta/openai, http://localhost:11434/v1.</summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public string Model { get; set; } = "gpt-4o-mini";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Có khóa thì dùng LlmAgent.</summary>
    public bool Enabled => !string.IsNullOrWhiteSpace(ApiKey);
}

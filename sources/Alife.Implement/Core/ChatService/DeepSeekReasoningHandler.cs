using System.Text;
using Alife.Framework;

namespace Alife.Implement;

/// <summary>
/// 一个特殊的 HttpClient 处理器，用于拦截 DeepSeek 的原始流，
/// 将其中的 reasoning_content 字段伪装成 content 字段，
/// 从而让旧版本的 Semantic Kernel / OpenAI SDK 能够读取到思考过程。
/// </summary>
public class DeepSeekReasoningHandler : DelegatingHandler
{
    public DeepSeekReasoningHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        // 仅对流式传输进行处理
        if (response.IsSuccessStatusCode &&
            response.Content.Headers.ContentType?.MediaType == "text/event-stream")
        {
            Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            response.Content = new StreamContent(new ReasoningStreamWrapper(stream));
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
        }

        return response;
    }

    class ReasoningStreamWrapper : Stream
    {
        readonly Stream innerStream;

        public ReasoningStreamWrapper(Stream innerStream)
        {
            this.innerStream = innerStream;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)

        {
            int bytesRead = await innerStream.ReadAsync(buffer, cancellationToken);
            if (bytesRead <= 0) return bytesRead;

            // 将读取到的字节转为字符串进行处理
            string text = Encoding.UTF8.GetString(buffer.Span.Slice(0, bytesRead));

            // 核心逻辑：将 reasoning_content 替换为 content，并加上特殊标记
            // 注意：DeepSeek 的 reasoning_content 通常在 content 之前返回，且 content 为 null
            if (text.Contains("\"reasoning_content\":\""))
            {
                // 将 "content":null 替换为 "content":"" 以防止 SDK 忽略
                text = text.Replace("\"content\":null", "\"content\":\"\"");
                // 将 "reasoning_content":"..." 替换为 "content":"__THINK__..."
                // 这样 SDK 就会把它当做普通内容读出来，我们在 ChatBot 里再剥离
                text = text.Replace("\"reasoning_content\":\"", $"\"content\":\"{ChatBot.ThinkContentPrefix}");
            }

            byte[] newBytes = Encoding.UTF8.GetBytes(text);
            newBytes.CopyTo(buffer);
            return newBytes.Length;
        }

        // 必须实现的其他 Stream 成员
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => innerStream.Length;
        public override long Position { get => innerStream.Position; set => throw new NotSupportedException(); }
        public override void Flush() => innerStream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("Use ReadAsync");
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

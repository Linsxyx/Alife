using Alife.Basic;
using System.ComponentModel;
using Alife.Framework;
using Alife.Function.Interpreter;
using Alife.Function.Vision;

namespace Alife.Implement;

public partial class VisionService
{
    static readonly VisionAnalyzer Analyzer;
    static VisionService()
    {
        Analyzer = new VisionAnalyzer();
    }
}
[Plugin("视觉感知", "让 AI 能够看到屏幕内容，理解图片，观察世界。")]
[Description("此服务让你拥有视觉感知能力：你可以截取屏幕画面并理解其内容，或者分析用户提供的图片。（注意！分析系统并不准确，所以你需要配合结果，自己洞察出真正的实际情况）")]
public partial class VisionService : InteractivePlugin<VisionService>
{
    /// <summary>
    /// 截取屏幕并进行视觉理解，将结果反馈给 AI。
    /// </summary>
    [XmlFunction("look_screen")]
    [Description("截取当前屏幕并根据需求进行视觉内容分析。")]
    public async Task LookScreen(XmlExecutorContext context, string query)
    {
        if (context.CallMode != CallMode.OneShot)
            return;

        string screenshotPath = AlifePlatform.Screenshot();
        Task<string> visionTask = Analyzer.QueryAsync(screenshotPath, query);
        string windowInfo = AlifePlatform.GetRunningWindowTitles();
        string visionInfo = await visionTask;

        Poke($"窗口信息：{windowInfo}\n视觉分析结果：{visionInfo}");
    }

    /// <summary>
    /// 分析指定路径的图片。
    /// </summary>
    [XmlFunction("look_image")]
    [Description("根据需求对指定的图片进行视觉内容分析。")]
    public async Task LookImage(XmlExecutorContext context, [Description("图片地址或网址")] string path, string query)
    {
        if (context.CallMode != CallMode.OneShot)
            return;

        try
        {
            // 处理网络图片
            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                path = await DownloadImageAsync(path);

            string result = await Analyzer.QueryAsync(path, query);
            Poke($"图片分析结果：{result}");
        }
        catch (Exception ex)
        {
            Poke($"图片处理失败：{ex.Message}");
        }

        async Task<string> DownloadImageAsync(string url)
        {
            const string Filename = "vision_download.png";
            string tempPath = $"{AlifePath.TempFolderPath}/{Filename}";
            
            using HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            // 针对腾讯多媒体服务器设置 Referer，防止 400/403
            if (url.Contains("multimedia.nt.qq.com.cn") || url.Contains("qpic.cn")) 
                request.Headers.Add("Referer", "https://q.qq.com/");
            using HttpResponseMessage response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            byte[] data = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(tempPath, data);
            return tempPath;
        }
    }

    readonly HttpClient httpClient = new();

    public VisionService(InterpreterService interpreterService)
    {
        interpreterService.RegisterHandler(this);
    }

}

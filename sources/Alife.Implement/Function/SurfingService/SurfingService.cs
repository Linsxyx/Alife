using System.ComponentModel;
using System.Text.Json;
using Alife.Framework;
using Alife.Function.Browser;
using Alife.Function.Interpreter;
using Microsoft.Extensions.DependencyInjection;

namespace Alife.Implement.Function;

[Plugin("网上冲浪", "让 AI 像人一样操控浏览器：打开网页、观察页面、点击、打字、滚动、执行脚本。")]
[Description(@"你拥有一个属于自己的、真实的、用户可见的浏览器窗口，你可以像真人一样去操作它。
提示：如果你遇到了需要验证、登录之类的页面，不要直接放弃，可以尝试让主人进行协助。")]
public class SurfingService(FunctionService functionService)
    : InteractivePlugin<SurfingService>, IDisposable
{
    readonly BrowserEngine browser = new();

    [XmlFunction("navigate")]
    [Description("在浏览器中打开指定网址。成功后会自动返回页面观察结果，无需再次调用 observe。")]
    public async Task Navigate(XmlExecutorContext context,
        [Description("要打开的网址")] string url)
    {
        if (context.CallMode != CallMode.OneShot)
            throw new Exception("请使用自闭合标签调用。");

        var result = await browser.NavigateAsync(url);
        if (result.Success)
        {
            string observation = await browser.ObserveAsync();
            Poke($"[Navigate] 已打开: {url}\n[Auto-Observe] 页面内容：\n{observation}");
        }
        else
        {
            Poke($"[Navigate] 加载失败 (HTTP {result.StatusCode})");
        }
    }


    [XmlFunction("observe")]
    [Description("观察当前页面：返回标题、URL、正文文本以及所有可交互元素的选择器。")]
    public async Task Observe(XmlExecutorContext context)
    {
        if (context.CallMode != CallMode.OneShot)
            throw new Exception("请使用自闭合标签调用。");

        string result = await browser.ObserveAsync();
        Poke($"[Observe] 页面状态：\n{result}");
    }

    [XmlFunction("click")]
    [Description("点击页面上的元素。")]
    public async Task Click(XmlExecutorContext context,
        [Description("CSS 选择器")] string selector)
    {
        if (context.CallMode != CallMode.OneShot)
            throw new Exception("请使用自闭合标签调用。");

        string result = await browser.ClickAsync(selector);
        Poke($"[Click] {result}");
    }

    [XmlFunction("type")]
    [Description("在输入框中打字。")]
    public async Task Type(XmlExecutorContext context,
        [Description("CSS 选择器")] string selector,
        [Description("要输入的文字")] string text,
        [Description("是否提交（回车），默认 true")] bool submit = true)
    {
        if (context.CallMode != CallMode.OneShot)
            throw new Exception("请使用自闭合标签调用。");

        string result = await browser.TypeAsync(selector, text, submit);
        Poke($"[Type] {result}");
    }

    [XmlFunction("scroll")]
    [Description("滚动页面。")]
    public async Task Scroll(XmlExecutorContext context,
        [Description("方向：up 或 down")] string direction,
        [Description("距离（像素），默认 500")] int pixels = 500)
    {
        if (context.CallMode != CallMode.OneShot)
            throw new Exception("请使用自闭合标签调用。");

        string result = await browser.ScrollAsync(direction, pixels);
        Poke($"[Scroll] {result}");
    }

    [XmlFunction("runjs")]
    [Description("在浏览器的控制台中执行js代码。")]
    public async Task ExecuteScript(XmlExecutorContext context, [XmlContent] string script = "")
    {
        if (context.CallMode != CallMode.Closing)
            return;

        // 诊断版：极简包装，直接 eval
        string escapedCode = JsonSerializer.Serialize(context.FullContent.Trim());
        string safeScript = $@"
        (function() {{
            const code = {escapedCode};
            try {{
                let r = eval(code);
                if (r instanceof Promise) return r.then(v => JSON.stringify(v));
                return JSON.stringify(r === undefined ? '(无返回值)' : r);
            }} catch(err) {{
                return JSON.stringify('JS_ERROR: ' + err.message);
            }}
        }})()";

        string result = await browser.ExecuteScriptAsync(safeScript);
        Poke($"[ExecuteScript][v2.2] 执行结果：\n{result}");
    }

    [XmlFunction("download")]
    [Description("下载文件到本地。")]
    public async Task Download(XmlExecutorContext context,
        [Description("下载链接")] string url,
        [Description("本地绝对路径")] string path)
    {
        if (context.CallMode != CallMode.OneShot)
            throw new Exception("请使用自闭合标签调用。");

        await BrowserEngine.DownloadFileAsync(url, path);
        Poke($"[Download] 文件已下载至：{path}");
    }

    public override async Task AwakeAsync(AwakeContext context)
    {
        await base.AwakeAsync(context);
        functionService.RegisterHandler(this, "runjs");
    }

    public void Dispose() => browser.Dispose();
}
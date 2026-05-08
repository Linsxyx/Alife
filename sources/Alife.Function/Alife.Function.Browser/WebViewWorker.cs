using Microsoft.Web.WebView2.WinForms;
using System.IO;

namespace Alife.Function.Browser;

/// <summary>
/// 在独立的 STA 线程中管理 WebView2，以支持后台执行浏览器任务。
/// 浏览器窗口默认可见，用户可实时观察 AI 的浏览行为并手动介入。
/// </summary>
public class WebViewWorker : IDisposable
{
    public Task<T> EnqueueAsync<T>(Func<WebView2, Task<T>> action)
    {
        TaskCompletionSource<T> tcs = new();

        jobs.Add(async () =>
        {
            try
            {
                if (initialized == false || webView == null)
                    throw new InvalidOperationException("WebView2 引擎尚未就绪或未安装。");

                T result = await action(webView);
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }
    
    readonly System.Collections.Concurrent.BlockingCollection<Func<Task>> jobs = new();
    WebView2? webView;
    Form? form;
    bool initialized;

    public WebViewWorker()
    {
        var thread = new Thread(RunLoop);
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
    }

    public void Dispose()
    {
        jobs.CompleteAdding();
        if (form is { IsDisposed: false })
        {
            form.Invoke((Action)(() => form.Close()));
        }
    }

    void RunLoop()
    {
        form = new Form
        {
            Text = "Alife Browser",
            Width = 1024,
            Height = 768,
            WindowState = FormWindowState.Normal,
            ShowInTaskbar = true,
            FormBorderStyle = FormBorderStyle.Sizable
        };

        webView = new WebView2 { Dock = DockStyle.Fill };
        form.Controls.Add(webView);

        form.Load += async (s, e) =>
        {
            try
            {
                // 指定一个绝对有写入权限的目录作为 WebView2 的数据文件夹
                string userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Alife", "WebView2Data");
                if (!Directory.Exists(userDataFolder)) Directory.CreateDirectory(userDataFolder);

                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
                await webView.EnsureCoreWebView2Async(env);
                
                webView.CoreWebView2.Settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36 Edge/122.0.0.0";
                
                webView.CoreWebView2.NewWindowRequested += (sender, ev) =>
                {
                    ev.Handled = true;
                    webView.CoreWebView2.Navigate(ev.Uri);
                };

                initialized = true;

                _ = Task.Run(() =>
                {
                    foreach (Func<Task> job in jobs.GetConsumingEnumerable())
                    {
                        if (form.IsDisposed) break;
                        try
                        {
                            Task task = form.Invoke(job);
                            task.Wait();
                        }
                        catch
                        {
                            // 忽略单个任务崩溃，保证队列继续运行
                        }
                    }
                });
            }
            catch
            {
                // 浏览器核心初始化失败
            }
        };

        Application.Run(form);
    }
}

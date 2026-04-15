using Alife.Function.QChat;
using NUnit.Framework;
using System.IO;
using System.Windows;

namespace Alife.Test.QChat;

[TestFixture]
public class QChatFunctionTests
{
    [OneTimeSetUp]
    public async Task Setup()
    {
        client = new OneBotClient(TestUrl);
        await client.ConnectAsync();
    }

    [Test, Order(1)]
    public async Task TestSimpleFlow()
    {
        Console.WriteLine($"已连接。Bot Id: {client.BotId}");

        // 接收第一个消息以识别目标
        OneBotMessageEvent? target = null;
        client.OnEventReceived += e => { if (e is OneBotMessageEvent m) target = m; };

        MessageBox.Show("请发送任意【私聊】消息以开始测试...", "简单测试");

        while (target == null) await Task.Delay(500);
        lastTargetId = target.UserId;

        // 原路返回回复
        await client.SendPrivateMessage(target.UserId, $"收到私聊：{target.RawMessage}");

        MessageBoxResult result = MessageBox.Show("Bot 是否成功回复了你的消息？", "人工验证", MessageBoxButton.YesNo);
        Assert.That(result, Is.EqualTo(MessageBoxResult.Yes));
    }

    [Test, Order(2)]
    public async Task TestFileUpload()
    {
        if (lastTargetId == 0) Assert.Ignore("请先运行上一个测试以锚定私聊对象。");

        string tempFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "upload_test.txt");
        await File.WriteAllTextAsync(tempFile, $"Simple Upload Test - {DateTime.Now}");

        await client.UploadPrivateFile(lastTargetId, tempFile, "测试文档.txt");

        MessageBoxResult result = MessageBox.Show("你的 QQ 是否成功收到了 '测试文档.txt'？", "文件发送验证", MessageBoxButton.YesNo);
        Assert.That(result, Is.EqualTo(MessageBoxResult.Yes));
    }

    [OneTimeTearDown]
    public async Task Teardown()
    {
        await client.DisposeAsync();
    }

    OneBotClient client = null!;
    long lastTargetId;
    const string TestUrl = "ws://127.0.0.1:3001";
}

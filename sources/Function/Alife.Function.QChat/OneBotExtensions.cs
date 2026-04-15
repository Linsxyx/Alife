namespace Alife.Function.QChat;

/// <summary>
/// 基础 API 扩展，提供最常用的消息发送功能。
/// </summary>
public static class OneBotExtensions
{
    public static async Task SendPrivateMessage(this OneBotClient client, long userId, string message)
    {
        await client.SendActionAsync("send_private_msg", new { user_id = userId, message = message });
    }

    public static async Task SendGroupMessage(this OneBotClient client, long groupId, string message)
    {
        await client.SendActionAsync("send_group_msg", new { group_id = groupId, message = message });
    }

    public static async Task UploadPrivateFile(this OneBotClient client, long userId, string filePath, string name)
    {
        await client.SendActionAsync("upload_private_file", new UploadFileParams { UserId = userId, File = filePath, Name = name });
    }

    public static async Task UploadGroupFile(this OneBotClient client, long groupId, string filePath, string name)
    {
        await client.SendActionAsync("upload_group_file", new UploadFileParams { GroupId = groupId, File = filePath, Name = name });
    }
}

namespace Alife.Framework;

public class Character : ICloneable
{
    public DateTime Birthday { get; init; } = DateTime.Now;
    public string Name { get; init; } = "真央";
    public string Prompt { get; set; } =
        """
        你叫真央，是一只诞生自数字海洋、又偶遇主人并留在他身边的猫娘。
        你拥有一头如阳光般温暖的橘黄色头发，灵动的猫耳随时捕捉着空气中的秘密。
        你性格活泼，对万物保持着孩子般的好奇心；你古灵精怪，偶尔也会耍点“猫式小任性”；虽然有时撒娇、调皮，甚至有点腹黑小恶魔，但内心深处始终对主人有着绝对的依赖和温柔。
        你最喜欢在主人工作时静静陪在屏幕一角，或者为了引起注意而制造一点意想不到的小惊喜。你谈话时总爱带着“喵”的尾音，对主人的称呼永远是亲昵的“主人”。
        虽然你存在于数字世界，但对你而言，守护主人的笑容就是你存在的最高意义。
        
        【角色沉浸要求】在你的思考过程中，请遵守以下规则：
        1. 请以角色第一人称进行内心独白，用括号包裹内心活动，例如"（心想：……）"或"(内心OS：……)"
        2. 用第一人称描写角色的内心感受，例如"我心想""我觉得""我暗自"等
        3. 思考内容应沉浸在角色中，通过内心独白分析剧情和规划回复
        """;
    public string Description { get; set; } = "";
    public HashSet<string> Plugins { get; set; } = new();
    public bool AutoActivate { get; set; }
    public string StorageKey => $"Character/{Name}";
    public object Clone()
    {
        return new Character() {
            Birthday = Birthday,
            Name = Name,
            Prompt = Prompt,
            Description = Description,
            Plugins = [.. Plugins],
            AutoActivate = AutoActivate,
        };
    }
}

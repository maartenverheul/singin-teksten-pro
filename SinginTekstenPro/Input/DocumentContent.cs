namespace SinginTekstenPro.Input;

enum ContentKind
{
    Text,
    Row,
}

sealed record ContentBlock(ContentKind Kind, IReadOnlyList<string> Lines);

sealed record DocumentContent(IReadOnlyList<ContentBlock> Blocks);

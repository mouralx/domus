import { Fragment } from 'react';

// Render a small set of prose formatting with React text nodes. Provider output
// never becomes HTML, and card navigation remains in the validated sources list.
function inline(text: string) {
  return text.split(/(`[^`\n]+`|\*\*[^*\n]+\*\*)/g).map((part, index) => {
    if (part.startsWith('**') && part.endsWith('**'))
      return <strong key={index}>{part.slice(2, -2)}</strong>;
    if (part.startsWith('`') && part.endsWith('`'))
      return <code key={index}>{part.slice(1, -1)}</code>;
    return <Fragment key={index}>{part}</Fragment>;
  });
}
export function AssistantText({ text }: { text: string }) {
  // A few models double-escape paragraph breaks inside their JSON text field.
  // Preserve literal escapes inside backticks, where they may be code or paths.
  const normalized = text
    .split(/(`[^`]*`)/g)
    .map((part) => (part.startsWith('`') ? part : part.replace(/\\r\\n|\\n/g, '\n')))
    .join('');
  const lines = normalized.replace(/\r\n/g, '\n').split('\n');
  const blocks: { kind: 'p' | 'ul' | 'ol'; lines: string[] }[] = [];
  for (const line of lines) {
    if (!line.trim()) {
      if (blocks.at(-1)?.lines.length) blocks.push({ kind: 'p', lines: [] });
      continue;
    }
    const bullet = line.match(/^\s*[-*]\s+(.+)$/);
    const number = line.match(/^\s*\d+[.)]\s+(.+)$/);
    const kind = bullet ? 'ul' : number ? 'ol' : 'p';
    if (blocks.at(-1)?.kind !== kind) blocks.push({ kind, lines: [] });
    if (!blocks.length) blocks.push({ kind, lines: [] });
    blocks.at(-1)!.lines.push(bullet?.[1] ?? number?.[1] ?? line);
  }
  return (
    <div className="assistant-prose">
      {blocks
        .filter((block) => block.lines.length)
        .map((block, index) => {
          if (block.kind === 'p') return <p key={index}>{inline(block.lines.join('\n'))}</p>;
          const List = block.kind;
          return (
            <List key={index}>
              {block.lines.map((line, item) => (
                <li key={item}>{inline(line)}</li>
              ))}
            </List>
          );
        })}
    </div>
  );
}

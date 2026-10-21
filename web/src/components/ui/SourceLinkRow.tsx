import Icon from "./Icon";

type SourceLinkRowProps = { title: string; url: string };

export default function SourceLinkRow({ title, url }: SourceLinkRowProps) {
  return (
    <a
      href={url}
      target="_blank"
      rel="noreferrer"
      className="mt-3 flex items-center gap-1 text-xs text-blue-600 hover:underline"
    >
      <Icon name="arrowUpRight" className="h-3.5 w-3.5" />
      {title}
    </a>
  );
}

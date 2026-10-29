/*
 * Os ícones da aplicação, desenhados aqui, no traço da Lucide: sempre ao
 * lado de uma palavra, que é quem diz o que é. Por isso ficam escondidos do
 * leitor de tela.
 */

const Desenhos = {
  marca: (
    <>
      <path d="M4 20V10l5-4 5 4v10" />
      <path d="M14 20v-6h6v6M9 20v-5" />
      <path d="M2 20h20" />
    </>
  ),
  funil: <path d="M3 4h18l-7 8.5V19l-4 2v-8.5z" />,
  clientes: (
    <>
      <rect x="4" y="3" width="16" height="18" rx="2" />
      <path d="M9 7h1M14 7h1M9 11h1M14 11h1M9 15h1M14 15h1M10 21v-3h4v3" />
    </>
  ),
  previsao: (
    <>
      <path d="M3 3v18h18" />
      <path d="M7 15l4-4 3 3 6-6" />
      <path d="M16 8h4v4" />
    </>
  ),
  empresa: (
    <>
      <path d="M6 22V4a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v18" />
      <path d="M6 12H4a2 2 0 0 0-2 2v8M18 9h2a2 2 0 0 1 2 2v11M10 6h4M10 10h4M10 14h4M10 18h4" />
    </>
  ),
  seta: <path d="m9 18 6-6-6-6" />,
  sair: (
    <>
      <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
      <path d="m16 17 5-5-5-5M21 12H9" />
    </>
  ),
} as const;

export type NomeDoIcone = keyof typeof Desenhos;

export function Icone({
  nome,
  className,
}: {
  readonly nome: NomeDoIcone;
  readonly className?: string | undefined;
}) {
  return (
    <svg
      className={className}
      viewBox="0 0 24 24"
      width="18"
      height="18"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      {Desenhos[nome]}
    </svg>
  );
}

export type HelpArticle = {
  id: string;
  category: string;
  en: [string, string];
  pt: [string, string];
};
export const helpCategories = [
  ['All topics', 'Todos os temas'],
  ['Account & appearance', 'Conta e aparência'],
  ['Workspaces & members', 'Espaços e membros'],
  ['Projects & personal tasks', 'Projetos e tarefas pessoais'],
  ['Cards & files', 'Cartões e ficheiros'],
  ['Workflow & organization', 'Processo e organização'],
  ['Dashboards & exports', 'Painéis e exportações'],
  ['Links & sharing', 'Ligações e partilha'],
  ['AI & assistants', 'IA e assistentes'],
  ['Automations', 'Automações'],
  ['Jira synchronization', 'Sincronização Jira'],
];
export const helpArticles: HelpArticle[] = [
  {
    id: 'signin',
    category: 'Account & appearance',
    en: [
      'How do I sign in or register?',
      'On the sign-in screen, choose Continue with Google or Continue with Microsoft. Follow the provider’s sign-in steps to open your workspace. You can also choose Continue with email to sign in or create an account. Google and Microsoft sign-in opens the provider’s secure sign-in page when enabled.',
    ],
    pt: [
      'Como inicio sessão ou crio uma conta?',
      'No ecrã de início de sessão, escolha Continuar com Google ou Continuar com Microsoft. Siga os passos do fornecedor para abrir o espaço de trabalho. Também pode escolher Continuar com email para iniciar sessão ou criar uma conta. O início de sessão Google ou Microsoft abre a página segura do fornecedor quando está ativado.',
    ],
  },
  {
    id: 'logout',
    category: 'Account & appearance',
    en: [
      'How do I log out?',
      'Choose Sign out at the bottom of the sidebar or open your avatar in the header and choose Sign out. On a small screen, open the navigation menu or use the avatar. You return to sign-in; saved work remains available when you return.',
    ],
    pt: [
      'Como termino a sessão?',
      'Escolha Terminar sessão no fundo do menu lateral ou abra o avatar no cabeçalho e escolha Terminar sessão. Num ecrã pequeno, abra o menu de navegação ou use o avatar. Regressa ao início de sessão; o trabalho guardado continua disponível quando voltar a entrar.',
    ],
  },
  {
    id: 'profile',
    category: 'Account & appearance',
    en: [
      'How do I change my name, email, or profile picture?',
      'Open your avatar in the header. Edit your name or use Change photo, review the preview and choose Save profile. Your sign-in email is read-only. JPG, PNG, WebP and GIF up to 2 MB are supported. Server accounts require a photo, which can be replaced. SVG is supported for the platform logo.',
    ],
    pt: [
      'Como altero o nome, o email ou a fotografia de perfil?',
      'Abra o avatar no cabeçalho. Edite o nome ou use Alterar fotografia, confirme a pré-visualização e escolha Guardar perfil. O email de início de sessão é apenas de leitura. São aceites JPG, PNG, WebP e GIF até 2 MB. As contas do servidor exigem uma fotografia, que pode ser substituída. O SVG é aceite para o logótipo da plataforma.',
    ],
  },
  {
    id: 'theme',
    category: 'Account & appearance',
    en: [
      'How do I switch between Light, Dark, and System?',
      'Use the theme selector in the header, in Settings, or on the sign-in screen. Light and Dark stay fixed. System follows your device appearance, including changes while Domus is open. Your choice is remembered across reloads.',
    ],
    pt: [
      'Como alterno entre Claro, Escuro e Sistema?',
      'Use o seletor de tema no cabeçalho, em Definições ou no ecrã de início de sessão. Claro e Escuro mantêm-se fixos. Sistema acompanha a aparência do dispositivo, incluindo alterações enquanto o Domus está aberto. A escolha é guardada ao recarregar.',
    ],
  },
  {
    id: 'language',
    category: 'Account & appearance',
    en: [
      'How do I change the language?',
      'Select EN or PT in the header for English (United States) or Portuguese (Portugal). Domus remembers the choice. Interface text and dates change language; names and descriptions you write stay as entered.',
    ],
    pt: [
      'Como altero o idioma?',
      'Selecione EN ou PT no cabeçalho para inglês dos Estados Unidos ou português de Portugal. O Domus guarda a escolha. Os textos da interface e as datas mudam de idioma; os nomes e as descrições que escreveu mantêm-se.',
    ],
  },
  {
    id: 'navigation',
    category: 'Account & appearance',
    en: [
      'How do I collapse the sidebar or use keyboard shortcuts?',
      'Use the arrow at the sidebar edge to collapse it to icons or expand it. Drag that edge to resize, or double-click to fit the labels. With the resize handle focused, Left/Right adjusts width, Home collapses and End fits the text. Your width is remembered. On mobile, use the header navigation button. Ctrl+K or Command+K focuses search; Escape closes the top dialog.',
    ],
    pt: [
      'Como recolho o menu lateral ou uso atalhos de teclado?',
      'Use a seta na margem do menu lateral para o reduzir a ícones ou expandir. Arraste essa margem para ajustar a largura ou faça duplo clique para mostrar os textos completos. Com o foco na margem de redimensionamento, Esquerda/Direita ajusta a largura, Home recolhe e End adapta ao texto. A largura fica guardada. No telemóvel, use o botão de navegação no cabeçalho. Ctrl+K ou Command+K coloca o foco na pesquisa; Escape fecha a caixa de diálogo superior.',
    ],
  },
  {
    id: 'notifications',
    category: 'Account & appearance',
    en: [
      'When do I receive assignment notifications, and how do I clear them?',
      'Open the bell in the header. A new assignment to your registered account creates a private notification in that workspace, including assignments imported from Jira. Unchanged assignments do not notify again, and old assignments are not backfilled. Dismiss one notification or use Clear notifications to clear your visible notifications, not another member’s private notices. Card history and workspace activity are preserved.',
    ],
    pt: [
      'Quando recebo notificações de atribuição e como as limpo?',
      'Abra o sino no cabeçalho. Uma nova atribuição à sua conta registada cria uma notificação privada nesse espaço, incluindo atribuições importadas do Jira. Atribuições inalteradas não voltam a notificar e as antigas não geram notificações retroativas. Dispense uma notificação ou use Limpar notificações para limpar as que lhe são apresentadas, não as notificações privadas de outros membros. O histórico dos cartões e a atividade do espaço são preservados.',
    ],
  },
  {
    id: 'workspaces',
    category: 'Workspaces & members',
    en: [
      'How do I create or switch workspaces?',
      'Click the workspace name in the sidebar. Enter a New workspace name and choose Create workspace, or select an existing workspace. Each workspace has its own projects, cards, members, and workflow settings. New workspaces include My activities.',
    ],
    pt: [
      'Como crio ou mudo de espaço de trabalho?',
      'Clique no nome do espaço no menu lateral. Introduza o nome do novo espaço e escolha Criar espaço de trabalho, ou selecione um espaço existente. Cada espaço tem os seus projetos, cartões, membros e definições de processo. Os novos espaços incluem As minhas atividades.',
    ],
  },
  {
    id: 'workspace-images',
    category: 'Workspaces & members',
    en: [
      'How do I change the workspace icon?',
      'Open Settings → Workspace → Workspace appearance. Choose Change icon, upload a JPG, PNG, or WebP image up to 10 MB, and review the preview. Choose Save workspace images. Remove icon restores the default after saving.',
    ],
    pt: [
      'Como altero o ícone do espaço?',
      'Abra Definições → Espaço de trabalho → Aparência do espaço de trabalho. Escolha Alterar ícone, carregue uma imagem JPG, PNG ou WebP até 10 MB e confirme a pré-visualização. Escolha Guardar imagens do espaço de trabalho. Remover ícone repõe a aparência inicial depois de guardar.',
    ],
  },
  {
    id: 'workspace-delete',
    category: 'Workspaces & members',
    en: [
      'How do I delete a workspace? Why is My Workspace protected?',
      'Open the workspace switcher and use the delete icon beside an additional workspace, then confirm. Deletion removes its projects, cards, history, definitions, and unshared files. Deleting the current workspace returns you to My Workspace. My Workspace always exists and cannot be deleted.',
    ],
    pt: [
      'Como elimino um espaço? Porque está My Workspace protegido?',
      'Abra o seletor de espaços e use o ícone de eliminar junto de um espaço adicional; confirme a operação. A eliminação remove os projetos, cartões, histórico, definições e ficheiros não partilhados. Se eliminar o espaço atual, regressa a My Workspace. My Workspace existe sempre e não pode ser eliminado.',
    ],
  },
  {
    id: 'members',
    category: 'Workspaces & members',
    en: [
      'How do I add or remove workspace members?',
      'Open Members and use the + Better, together. card to invite a teammate. Enter their name and email, confirm, then copy the invitation link from their member card and send it to them. They must sign in with the invited email and accept the invitation. Invitations are not sent automatically. Remove a member from their card with confirmation: their assignments are cleared and the cards are preserved. The owner cannot be removed.',
    ],
    pt: [
      'Como adiciono ou removo membros do espaço?',
      'Abra Membros e use o cartão + Melhor, em conjunto. para convidar uma pessoa. Introduza o nome e o email, confirme e copie a ligação de convite no cartão do membro para a enviar. A pessoa tem de iniciar sessão com o email convidado e aceitar o convite. Os convites não são enviados automaticamente. Remova um membro no respetivo cartão mediante confirmação: as atribuições são limpas e os cartões preservados. O proprietário não pode ser removido.',
    ],
  },
  {
    id: 'storage',
    category: 'Workspaces & members',
    en: [
      'Where is my work saved?',
      'Your work and files are stored by the server in PostgreSQL. Card edits need Save task; settings have their own Save buttons. Sign in on another device to access your workspaces. If someone else changes a workspace before your save, refresh and review the latest version before applying your changes.',
    ],
    pt: [
      'Onde é guardado o meu trabalho?',
      'O trabalho e os ficheiros são guardados pelo servidor em PostgreSQL. As edições dos cartões precisam de Guardar tarefa; as definições têm botões próprios. Inicie sessão noutro dispositivo para aceder aos seus espaços. Se outra pessoa alterar um espaço antes de guardar, atualize e reveja a versão mais recente antes de aplicar as alterações.',
    ],
  },
  {
    id: 'projects',
    category: 'Projects & personal tasks',
    en: [
      'How do I create and open projects?',
      'Choose New project in the sidebar, enter a name and description, choose an icon, and choose Create project. Click a project’s icon in the directory to change it. Projects opens the directory with Live and Archived tabs. Select a project there or use its shortcut in the sidebar to open it.',
    ],
    pt: [
      'Como crio e abro projetos?',
      'Escolha Novo projeto no menu lateral, introduza o nome e a descrição, escolha um ícone e selecione Criar projeto. Clique no ícone de um projeto no diretório para o alterar. Projetos abre o diretório com os separadores Ativos e Arquivados. Selecione um projeto no diretório ou use o atalho no menu lateral para o abrir.',
    ],
  },
  {
    id: 'personal',
    category: 'Projects & personal tasks',
    en: [
      'What is the difference between My tasks and My activities?',
      'My tasks shows cards assigned to you across active projects in this workspace. My activities is your permanent project for everyday work. Both open in Board view; use the tabs for List, Calendar or Dashboard. Cards created from My tasks go into My activities. Archived projects are excluded.',
    ],
    pt: [
      'Qual é a diferença entre As minhas tarefas e As minhas atividades?',
      'As minhas tarefas mostra os cartões que lhe estão atribuídos nos projetos ativos deste espaço. As minhas atividades é o projeto permanente para o trabalho diário. Ambos abrem na vista Quadro; use os separadores para Lista, Calendário ou Painel. Os cartões criados em As minhas tarefas ficam em As minhas atividades. Os projetos arquivados são excluídos.',
    ],
  },
  {
    id: 'archive',
    category: 'Projects & personal tasks',
    en: [
      'How do I archive or restore a project?',
      'Use Archive project in the project options menu or the archive action in Projects. Confirm to move it to Archived while retaining its cards, files, and history. Open Projects → Archived and use Restore project to make it live again. My activities cannot be archived.',
    ],
    pt: [
      'Como arquivo ou restauro um projeto?',
      'Use Arquivar projeto no menu de opções do projeto ou a ação de arquivo em Projetos. Confirme para o mover para Arquivados, preservando cartões, ficheiros e histórico. Abra Projetos → Arquivados e use Restaurar projeto para o tornar ativo novamente. As minhas atividades não pode ser arquivado.',
    ],
  },
  {
    id: 'delete-project',
    category: 'Projects & personal tasks',
    en: [
      'How do I delete a project?',
      'Use Delete project in the project directory or project options menu and confirm. This permanently removes its cards, history, swimlanes, and files not referenced elsewhere. Choose archive instead when you want to keep the project for later. My activities cannot be deleted or renamed.',
    ],
    pt: [
      'Como elimino um projeto?',
      'Use Eliminar projeto no diretório ou no menu de opções do projeto e confirme. A operação remove definitivamente os cartões, histórico, faixas e ficheiros sem outras referências. Escolha arquivar se quiser guardar o projeto para mais tarde. As minhas atividades não pode ser eliminado nem renomeado.',
    ],
  },
  {
    id: 'card-edit',
    category: 'Cards & files',
    en: [
      'How do I create, edit, or save a card?',
      'Open a project and choose Add task, or click an existing card in the board, list, calendar, or dashboard. The right-side panel shows the associated project above the title. Edit the details and choose Save task. Closing the panel without saving discards the pending edits. Card IDs are generated in uppercase.',
    ],
    pt: [
      'Como crio, edito ou guardo um cartão?',
      'Abra um projeto e escolha Adicionar tarefa, ou clique num cartão no quadro, na lista, no calendário ou num painel. O painel lateral direito mostra o projeto associado acima do título. Edite os detalhes e escolha Guardar tarefa. Fechar o painel sem guardar descarta as alterações pendentes. Os identificadores dos cartões são gerados em maiúsculas.',
    ],
  },
  {
    id: 'card-copy-delete',
    category: 'Cards & files',
    en: [
      'How do I duplicate or delete a card?',
      'Open the card and use Duplicate task or Delete task at the bottom of the Details panel. A duplicate gets a new ID and its own history. Delete task removes the card immediately, so use it only when you no longer need the card.',
    ],
    pt: [
      'Como duplico ou elimino um cartão?',
      'Abra o cartão e use Duplicar tarefa ou Eliminar tarefa no fundo do painel Detalhes. A cópia recebe um novo identificador e histórico próprio. Eliminar tarefa remove o cartão imediatamente; use esta ação apenas quando já não precisar dele.',
    ],
  },
  {
    id: 'assignees',
    category: 'Cards & files',
    en: [
      'How do I assign a card to several people?',
      'Open the card, find Assignees, and select each member who should be responsible. Select a checked member again to remove the assignment. A card can have several assignees or none. Choose Save task to apply the changes.',
    ],
    pt: [
      'Como atribuo um cartão a várias pessoas?',
      'Abra o cartão, encontre Responsáveis e selecione cada membro que deve ficar responsável. Volte a selecionar um membro assinalado para retirar a atribuição. Um cartão pode ter vários responsáveis ou nenhum. Escolha Guardar tarefa para aplicar as alterações.',
    ],
  },
  {
    id: 'dates',
    category: 'Cards & files',
    en: [
      'Do cards need a due date? How do I remove one?',
      'Due date is optional. Leave it empty when creating a card, or clear the date in the card panel and save. Undated cards stay on the board and list but do not appear in the calendar or count as overdue. Priority can be Low, Medium, or High independently of the date.',
    ],
    pt: [
      'Os cartões precisam de data limite? Como a removo?',
      'A data limite é opcional. Deixe-a vazia ao criar o cartão ou limpe a data no painel do cartão e guarde. Os cartões sem data continuam no quadro e na lista, mas não aparecem no calendário nem contam como atrasados. A prioridade pode ser Baixa, Média ou Alta, independentemente da data.',
    ],
  },
  {
    id: 'files',
    category: 'Cards & files',
    en: [
      'How do I attach, download, or remove documents?',
      'Open card Details and find the attachments area. Browse or drop files of up to 25 MB each. Choose Save task to keep the attachments. Use the download action to get the original file, or the remove action and then Save task to detach it.',
    ],
    pt: [
      'Como anexo, transfiro ou removo documentos?',
      'Abra Detalhes do cartão e encontre a área de anexos. Selecione ou arraste ficheiros até 25 MB cada. Escolha Guardar tarefa para manter os anexos. Use a ação de transferência para obter o ficheiro original, ou a ação de remover seguida de Guardar tarefa para o desanexar.',
    ],
  },
  {
    id: 'checklist',
    category: 'Cards & files',
    en: [
      'How do I use checklists and comments?',
      'In card Details, type a checklist item and press Enter. Check completed items or remove items you no longer need. Under Conversation, write a comment and use the send arrow. Choose Save task to keep checklist changes and comments.',
    ],
    pt: [
      'Como uso listas de verificação e comentários?',
      'Em Detalhes, escreva um item da lista de verificação e prima Enter. Assinale os itens concluídos ou remova os que já não precisa. Em Conversa, escreva um comentário e use a seta de envio. Escolha Guardar tarefa para conservar as alterações e os comentários.',
    ],
  },
  {
    id: 'history',
    category: 'Cards & files',
    en: [
      'Where can I see a card’s history?',
      'Open the card and select History next to Details. Entries show the actor, time, and saved changes, with the newest first. Status moves, assignments, labels, checklists, comments, files, and other saved edits appear here. Unsaved changes are not history entries.',
    ],
    pt: [
      'Onde vejo o histórico de um cartão?',
      'Abra o cartão e selecione Histórico junto de Detalhes. As entradas mostram o autor, a data e as alterações guardadas, começando pelas mais recentes. Movimentos de estado, atribuições, etiquetas, listas de verificação, comentários, ficheiros e outras edições guardadas aparecem aqui. Alterações por guardar não são entradas do histórico.',
    ],
  },
  {
    id: 'views',
    category: 'Workflow & organization',
    en: [
      'How do Board, List, and Calendar differ?',
      'Projects and My tasks open in Board view. Board arranges cards in columns and project swimlanes; choose Group by for Status or Bucket. List is a compact alternative with grouping controls. Calendar places dated cards on their due dates; use its arrows to change month. Dashboard shows metrics. The tabs let you switch while working.',
    ],
    pt: [
      'Qual é a diferença entre Quadro, Lista e Calendário?',
      'Os projetos e As minhas tarefas abrem na vista Quadro. O quadro organiza cartões em colunas e faixas dos projetos; escolha Agrupar por Estado ou Grupo. Lista é uma alternativa compacta com opções de agrupamento. Calendário coloca os cartões com data nos respetivos dias; use as setas para mudar de mês. Painel mostra métricas. Pode alternar nos separadores enquanto trabalha.',
    ],
  },
  {
    id: 'statuses',
    category: 'Workflow & organization',
    en: [
      'How do I define statuses and mark work as completed?',
      'Open the ⋯ options beside Add task and choose Manage statuses. Add, rename, color and reorder statuses, then save. Mark the statuses that represent completed work so dashboards count cards correctly. Drag a card to a status column or edit its Status. The destination is highlighted while dragging. At least one status must remain.',
    ],
    pt: [
      'Como defino estados e marco trabalho como concluído?',
      'Abra as opções ⋯ junto de Adicionar tarefa e escolha Gerir estados. Adicione, renomeie, escolha cores e ordene os estados; guarde. Marque os estados que representam trabalho concluído para que os painéis contem os cartões corretamente. Arraste um cartão para uma coluna de estado ou edite o Estado. O destino fica realçado durante o arrasto. Tem de permanecer pelo menos um estado.',
    ],
  },
  {
    id: 'buckets',
    category: 'Workflow & organization',
    en: [
      'How do buckets work?',
      'Open ⋯ beside Add task → Manage buckets to add, rename, color or reorder groups. Buckets are shared by the workspace and independent of status. When buckets exist, cards without one move to the first bucket and no No bucket column is shown, including in My tasks. Reordering changes which bucket is first. Group the board by Bucket to drag cards between columns, or choose the bucket in card Details. Clear an in-use bucket from its cards before deleting it.',
    ],
    pt: [
      'Como funcionam os grupos?',
      'Abra ⋯ junto de Adicionar tarefa → Gerir grupos para adicionar, renomear, escolher cores ou ordenar. Os grupos pertencem ao espaço e são independentes do estado. Quando existem grupos, os cartões sem grupo passam para o primeiro e deixa de aparecer a coluna Sem grupo, incluindo em As minhas tarefas. A ordenação define qual é o primeiro. Agrupe o quadro por Grupo para arrastar cartões entre colunas ou escolha o grupo nos Detalhes do cartão. Retire os cartões de um grupo em uso antes de o eliminar.',
    ],
  },
  {
    id: 'labels',
    category: 'Workflow & organization',
    en: [
      'How do I manage and apply labels?',
      'Use Manage labels above the board or inside a card. Create labels, choose their colors, rename, and reorder them, then save. Names must be unique within the workspace, ignoring capitalization and surrounding spaces: ADMO and admo are the same label. Jira imports reuse existing labels without changing their spelling or color. Select labels in card Details and Save task. A label in use must be removed from its cards before deletion.',
    ],
    pt: [
      'Como giro e aplico etiquetas?',
      'Use Gerir etiquetas acima do quadro ou dentro de um cartão. Crie etiquetas, escolha cores, renomeie e ordene; depois guarde. Os nomes têm de ser únicos no espaço, ignorando maiúsculas e espaços nas extremidades: ADMO e admo são a mesma etiqueta. As importações Jira reutilizam etiquetas existentes sem alterar a grafia ou a cor. Selecione etiquetas em Detalhes e escolha Guardar tarefa. Antes de eliminar uma etiqueta em uso, retire-a dos cartões.',
    ],
  },
  {
    id: 'swimlanes',
    category: 'Workflow & organization',
    en: [
      'How do I add, collapse, or move cards between swimlanes?',
      'Open ⋯ beside Add task → Manage swimlanes. Add, name and order rows, then save. Swimlanes belong to their project. When configured, cards without a row move to their project’s first swimlane; projects without rows receive an initial row when the workspace uses swimlanes. No unassigned row is shown on any board, including My tasks. Without swimlanes, no empty row heading is displayed. Click a heading to collapse or expand a row. Drag cards between rows or choose a row in card Details.',
    ],
    pt: [
      'Como adiciono ou recolho faixas e movo cartões entre elas?',
      'Abra ⋯ junto de Adicionar tarefa → Gerir faixas. Adicione, nomeie e ordene as linhas; guarde. As faixas pertencem ao respetivo projeto. Quando estão configuradas, os cartões sem faixa passam para a primeira do seu projeto; os projetos sem faixas recebem uma linha inicial quando o espaço usa faixas. Não aparece uma linha sem faixa em nenhum quadro, incluindo As minhas tarefas. Sem faixas, não aparece um cabeçalho de linha vazio. Clique no cabeçalho para recolher ou expandir. Arraste cartões entre linhas ou escolha a faixa nos Detalhes do cartão.',
    ],
  },
  {
    id: 'filters',
    category: 'Workflow & organization',
    en: [
      'How do I find or filter cards?',
      'Header search matches card titles, IDs or labels. Open Filter to narrow by priority, assignee, bucket, swimlane, status or completion/overdue metrics. Click outside the filter or options menu, or press Escape, to close it. Clear filters restores the view. My tasks starts with your assignments across active projects and resets project filters when opened.',
    ],
    pt: [
      'Como encontro ou filtro cartões?',
      'A pesquisa do cabeçalho encontra títulos, IDs ou etiquetas dos cartões. Abra Filtrar para restringir por prioridade, responsável, grupo, faixa, estado ou métricas de conclusão e atraso. Clique fora do filtro ou menu de opções, ou prima Escape, para o fechar. Limpar filtros repõe a vista. As minhas tarefas começa com as suas atribuições nos projetos ativos e repõe os filtros de projeto ao abrir.',
    ],
  },
  {
    id: 'dashboards',
    category: 'Dashboards & exports',
    en: [
      'Where can I see project and workspace metrics?',
      'Overview is the workspace home page and shows metrics across active projects. Its Bucket and Swimlane filters sit beside the card count and Export PDF. A project’s Dashboard tab covers its cards; My tasks has a dashboard for your assignments. Click a KPI, chart day, status bar or performance row to inspect matching cards.',
    ],
    pt: [
      'Onde vejo as métricas dos projetos e do espaço?',
      'Visão geral é a página inicial do espaço e mostra métricas dos projetos ativos. Os filtros Grupo e Faixa ficam junto da contagem de cartões e de Exportar PDF. O separador Painel de um projeto cobre os seus cartões; As minhas tarefas tem um painel das suas atribuições. Clique num indicador, dia do gráfico, barra de estado ou linha de desempenho para consultar os cartões correspondentes.',
    ],
  },
  {
    id: 'metrics',
    category: 'Dashboards & exports',
    en: [
      'How are completion, overdue, and workload calculated?',
      'Completed cards are those in a status marked complete. Overdue means an open card with a due date before today; cards without dates never count as overdue. Workload counts an open card once for each assignee, so shared assignments may total more than the unique card count. Archived projects are excluded from workspace metrics.',
    ],
    pt: [
      'Como são calculados a conclusão, o atraso e a carga de trabalho?',
      'Os cartões concluídos estão num estado marcado como concluído. Em atraso significa um cartão aberto com data limite anterior a hoje; cartões sem data nunca contam como atrasados. A carga de trabalho conta um cartão aberto por cada responsável, pelo que o total pode superar o número de cartões únicos. Os projetos arquivados não entram nas métricas do espaço.',
    ],
  },
  {
    id: 'dashboard-filters',
    category: 'Dashboards & exports',
    en: [
      'How do I see bucket and swimlane performance?',
      'Use the Bucket and Swimlane selectors inside a dashboard. KPIs, charts, and performance rows follow these filters. Bucket and swimlane rows show total, open, completed, and late cards. Use Clear dashboard filters to restore the full dashboard scope. List grouping also shows per-group counts.',
    ],
    pt: [
      'Como vejo o desempenho de grupos e faixas?',
      'Use os seletores Grupo e Faixa dentro de um painel. Os indicadores, gráficos e linhas de desempenho acompanham estes filtros. As linhas mostram cartões totais, abertos, concluídos e atrasados. Use Limpar filtros do painel para repor o âmbito completo. O agrupamento da lista também mostra contagens por conjunto.',
    ],
  },
  {
    id: 'pdf',
    category: 'Dashboards & exports',
    en: [
      'How do I export a dashboard as PDF?',
      'Open a project dashboard, My tasks dashboard, or Overview and choose Export PDF. The report includes the current scope and filters, KPIs, charts, workflow counts, workload, bucket/swimlane performance, checklists, and recent card activity. It downloads in the selected language with a date and page numbers.',
    ],
    pt: [
      'Como exporto um painel para PDF?',
      'Abra o painel de um projeto, de As minhas tarefas ou a Visão geral e escolha Exportar PDF. O relatório inclui o âmbito e filtros atuais, indicadores, gráficos, contagens por estado, carga de trabalho, desempenho de grupos/faixas, listas de verificação e atividade recente. É transferido no idioma selecionado, com data e números de página.',
    ],
  },
  {
    id: 'json',
    category: 'Dashboards & exports',
    en: [
      'How do I export project data?',
      'Open a project, select the three-dot Project options menu, and choose Export project. A JSON file downloads with the project and its cards. Attachments are represented by metadata; their file contents are not included in the JSON export.',
    ],
    pt: [
      'Como exporto os dados de um projeto?',
      'Abra um projeto, selecione o menu de três pontos Opções do projeto e escolha Exportar projeto. É transferido um ficheiro JSON com o projeto e os cartões. Os anexos aparecem como metadados; o conteúdo dos ficheiros não é incluído na exportação JSON.',
    ],
  },
  {
    id: 'card-url',
    category: 'Links & sharing',
    en: [
      'How do I get a direct URL for a card?',
      'Open a saved card and choose Copy link at the top of the side panel. The browser address also updates to the card URL. Opening it selects the workspace and project and opens that card. Direct links do not grant access to another account. Existing links with lowercase card IDs still work.',
    ],
    pt: [
      'Como obtenho uma ligação direta para um cartão?',
      'Abra um cartão guardado e escolha Copiar ligação no topo do painel lateral. O endereço do navegador também passa a ser a ligação do cartão. Ao abri-la, são selecionados o espaço e o projeto e abre-se esse cartão. Uma ligação direta não dá acesso a outra conta. As ligações antigas com identificadores em minúsculas continuam a funcionar.',
    ],
  },
  {
    id: 'share',
    category: 'Links & sharing',
    en: [
      'How do I share a card for someone to view?',
      'Open a saved card and choose Share link. Select Anyone signed in with the link or Workspace members only, choose an expiration, and Create share link. Copy the generated link. It opens a read-only card with details, files, and history after sign-in; it does not allow editing.',
    ],
    pt: [
      'Como partilho um cartão para outra pessoa o consultar?',
      'Abra um cartão guardado e escolha Ligação de partilha. Selecione Qualquer pessoa com sessão iniciada e a ligação ou Apenas membros do espaço de trabalho, escolha a validade e prima Criar ligação de partilha. Copie a ligação gerada. Após iniciar sessão, abre um cartão apenas de leitura com detalhes, ficheiros e histórico; não permite editar.',
    ],
  },
  {
    id: 'revoke',
    category: 'Links & sharing',
    en: [
      'How do I change access, set expiration, or revoke a share link?',
      'Reopen Share link on the card. Change the audience or expiration and use Replace link with these settings; copy and send the new link. The previous link stops working. Revoke link disables it without creating a replacement. Expired links show that the card is unavailable.',
    ],
    pt: [
      'Como altero o acesso, a validade ou revogo uma ligação?',
      'Volte a abrir Ligação de partilha no cartão. Altere o público ou a validade e use Substituir ligação com estas definições; copie e envie a nova ligação. A anterior deixa de funcionar. Revogar ligação desativa-a sem criar outra. As ligações expiradas mostram que o cartão está indisponível.',
    ],
  },
  {
    id: 'share-unavailable',
    category: 'Links & sharing',
    en: [
      'Why can’t someone open my shared card?',
      'Check that the link has not expired or been revoked, the card still exists, and the person is signed in with an allowed account. The link must point to a server the recipient can reach. A localhost address only works on the computer running Domus; use your deployed application address for other devices.',
    ],
    pt: [
      'Porque não consegue alguém abrir o meu cartão partilhado?',
      'Verifique se a ligação não expirou nem foi revogada, se o cartão existe e se a pessoa iniciou sessão com uma conta permitida. A ligação deve apontar para um servidor acessível ao destinatário. Um endereço localhost só funciona no computador que executa o Domus; use o endereço da aplicação publicada para outros dispositivos.',
    ],
  },
  {
    id: 'registration-setup',
    category: 'Account & appearance',
    en: [
      'What must I complete before opening my workspace?',
      'Email registration needs your name, email and a password of 12–200 characters. Finish the guided setup: choose your Gravatar photo if available or upload a JPG, PNG, WebP or GIF under 2 MB, then set up two-factor authentication. Google and Microsoft accounts also complete the required account setup. An invitation or shared-card link does not bypass these steps. Provider sign-in is available only when enabled by the installation administrator.',
    ],
    pt: [
      'O que tenho de concluir antes de abrir o espaço?',
      'O registo por email requer nome, email e uma palavra-passe de 12–200 caracteres. Conclua a configuração guiada: escolha a fotografia do Gravatar, se disponível, ou carregue JPG, PNG, WebP ou GIF até 2 MB; depois configure a autenticação de dois fatores. As contas Google e Microsoft também concluem a configuração obrigatória. Um convite ou uma ligação de partilha não dispensa estes passos. O início de sessão por fornecedor só está disponível quando ativado pelo administrador da instalação.',
    ],
  },
  {
    id: 'two-factor',
    category: 'Account & appearance',
    en: [
      'How do I set up two-factor authentication?',
      'During account setup, choose Set up authenticator and enter your current password if requested. In Microsoft Authenticator or another compatible app, add an account by scanning the QR code or entering the setup key. Enter its six-digit code and confirm before the setup expires in ten minutes. Download or securely store the recovery codes, then confirm that you saved them. Later sign-ins require an authenticator or recovery code. Two-factor authentication is required and cannot be disabled in the profile.',
    ],
    pt: [
      'Como configuro a autenticação de dois fatores?',
      'Durante a configuração da conta, escolha Configurar autenticador e introduza a palavra-passe atual, se pedida. No Microsoft Authenticator ou noutra aplicação compatível, adicione uma conta lendo o código QR ou introduzindo a chave de configuração. Introduza o código de seis dígitos e confirme antes de a configuração expirar, ao fim de dez minutos. Transfira ou guarde os códigos de recuperação num local seguro e confirme que os guardou. Os inícios de sessão seguintes exigem um código do autenticador ou de recuperação. A autenticação de dois fatores é obrigatória e não pode ser desativada no perfil.',
    ],
  },
  {
    id: 'recovery-codes',
    category: 'Account & appearance',
    en: [
      'What if I lose my authenticator or need new recovery codes?',
      'At sign-in, enter one of your saved recovery codes instead of an authenticator code. Each recovery code works once. In Profile → Two-factor authentication, check how many remain or choose Generate recovery codes. Supply your current password when requested and an authenticator or recovery code. Generating a new set invalidates every previous code; download and store the new set immediately because it is not shown again. There is no self-service recovery if you lose both your authenticator and all recovery codes; contact your installation administrator for assistance.',
    ],
    pt: [
      'E se perder o autenticador ou precisar de novos códigos de recuperação?',
      'Ao iniciar sessão, introduza um código de recuperação guardado em vez do código do autenticador. Cada código de recuperação funciona uma vez. Em Perfil → Autenticação de dois fatores, consulte quantos restam ou escolha Gerar códigos de recuperação. Introduza a palavra-passe atual, quando pedida, e um código do autenticador ou de recuperação. Um novo conjunto invalida todos os códigos anteriores; transfira-o e guarde-o imediatamente, pois não volta a ser apresentado. Não existe recuperação autónoma se perder o autenticador e todos os códigos; contacte o administrador da instalação para obter ajuda.',
    ],
  },
  {
    id: 'password',
    category: 'Account & appearance',
    en: [
      'How do I change my password?',
      'Open Profile → Change password. Enter your current password, a new password of 12–200 characters, its confirmation, and an authenticator or recovery code. Save new password keeps your current session and signs out your other sessions. Google-only or Microsoft-only accounts manage their password with that provider. There is no email-based Forgot password flow in Domus; changing a password here requires the current password and second factor.',
    ],
    pt: [
      'Como altero a palavra-passe?',
      'Abra Perfil → Alterar palavra-passe. Introduza a palavra-passe atual, uma nova de 12–200 caracteres, a confirmação e um código do autenticador ou de recuperação. Guardar nova palavra-passe mantém a sessão atual e termina as restantes sessões. As contas exclusivamente Google ou Microsoft gerem a palavra-passe nesse fornecedor. O Domus não tem recuperação de palavra-passe por email; a alteração aqui exige a palavra-passe atual e o segundo fator.',
    ],
  },
  {
    id: 'platform-appearance',
    category: 'Account & appearance',
    en: [
      'How do I customize the platform name, colors and typography?',
      'A platform administrator opens Settings → Workspace → Platform appearance. Set the name, primary and accent colors, light/dark backgrounds, panel colors, text and borders, plus sidebar background and text. Use color pickers or #RRGGBB hexadecimal values; Automatic removes an optional override. Choose the default, system, DM Sans or Manrope font, text size from 90–120%, and corner rounding from 0–16. Text colors are adjusted for readability. These controls style navigation, views, forms, dialogs and sign-in screens; status, priority, label and other content colors remain independent.',
    ],
    pt: [
      'Como personalizo o nome, as cores e a tipografia da plataforma?',
      'Um administrador da plataforma abre Definições → Espaço de trabalho → Aparência da plataforma. Defina o nome, as cores principal e de destaque, fundos claros/escuros, painéis, texto e contornos, além do fundo e texto do menu lateral. Use os seletores de cor ou valores hexadecimais #RRGGBB; Automático remove uma personalização opcional. Escolha a tipografia predefinida, do sistema, DM Sans ou Manrope, o tamanho do texto entre 90–120% e o arredondamento entre 0–16. As cores do texto são ajustadas para legibilidade. Estas opções aplicam-se à navegação, vistas, formulários, diálogos e início de sessão; as cores de estados, prioridades, etiquetas e outros conteúdos permanecem independentes.',
    ],
  },
  {
    id: 'platform-logo',
    category: 'Account & appearance',
    en: [
      'Which logo formats can I upload, including SVG?',
      'In Settings → Workspace → Platform appearance, upload an Expanded sidebar logo and an optional Collapsed sidebar logo. Without a collapsed upload, the main logo is reused. Other screens and mobile navigation use the main logo. Both accept PNG, JPG, GIF, WebP or SVG up to 2 MB; SVG is converted to transparent PNG with a 1024-pixel longest edge. Turn off Show name beside logo for a centered logo without visible text. The name remains required for the browser title and accessibility. Review the preview, then Save platform appearance. Remove logo restores the built-in symbol; removing the collapsed logo restores the main-logo fallback. Workspace icons are configured separately under Workspace appearance.',
    ],
    pt: [
      'Que formatos de logótipo posso carregar, incluindo SVG?',
      'Em Definições → Espaço de trabalho → Aparência da plataforma, carregue um Logótipo do menu lateral expandido e, opcionalmente, um Logótipo do menu lateral recolhido. Sem este último, é reutilizado o principal. Os outros ecrãs e a navegação móvel usam o principal. Ambos aceitam PNG, JPG, GIF, WebP ou SVG até 2 MB; o SVG é convertido para PNG transparente com 1024 píxeis no lado maior. Desative Mostrar nome junto do logótipo para um logótipo centrado sem texto visível. O nome continua obrigatório para o título do navegador e acessibilidade. Confirme a pré-visualização e escolha Guardar aparência da plataforma. Remover logótipo repõe o símbolo original; remover o recolhido repõe a utilização do principal. Os ícones dos espaços configuram-se separadamente em Aparência do espaço de trabalho.',
    ],
  },
  {
    id: 'platform-publish',
    category: 'Account & appearance',
    en: [
      'When do appearance changes take effect, and how do I reset them?',
      'The light/dark preview is private to your draft: nothing is published until Save platform appearance. Open tabs refresh branding within about a minute; reload to see it immediately. A personal Light, Dark or System preference takes precedence over the platform default. Reset to defaults changes only the draft and also needs saving. If another administrator saves first, reload the saved settings and review your edits rather than overwrite their changes. Platform-wide appearance requires a configured platform administrator, not just workspace ownership.',
    ],
    pt: [
      'Quando são aplicadas as alterações de aparência e como as reponho?',
      'A pré-visualização clara/escura pertence apenas ao rascunho: nada é publicado antes de Guardar aparência da plataforma. Os separadores abertos atualizam a identidade em cerca de um minuto; recarregue para a ver imediatamente. A preferência pessoal Claro, Escuro ou Sistema prevalece sobre a predefinição da plataforma. Repor predefinições só altera o rascunho e também exige guardar. Se outro administrador guardar primeiro, recarregue as definições e reveja as suas alterações em vez de as sobrepor. A aparência global exige um administrador da plataforma configurado, não apenas a propriedade de um espaço.',
    ],
  },
  {
    id: 'permissions',
    category: 'Workspaces & members',
    en: [
      'Why are some settings or actions unavailable to me?',
      'Workspace members collaborate on cards and workflows. The workspace owner manages workspace settings, membership, deletion and project Jira connections. Platform administrators manage global appearance and Jira server approvals; this is a separate permission. Pending invitees must accept their invitation before joining or being mapped as a Jira assignee. Invitations are copied and sent by the owner, not emailed automatically. Personal workspaces and My activities have additional deletion restrictions. Jira-managed read-only cards also prevent changes or deletion that would remove those cards.',
    ],
    pt: [
      'Porque não tenho acesso a algumas definições ou ações?',
      'Os membros colaboram nos cartões e processos. O proprietário gere as definições, membros e eliminação do espaço, e as ligações Jira dos projetos. Os administradores da plataforma gerem a aparência global e a aprovação de servidores Jira; é uma permissão distinta. Os convidados têm de aceitar o convite antes de aderirem ou serem associados a um responsável Jira. O proprietário copia e envia os convites; não são enviados automaticamente por email. Os espaços pessoais e As minhas atividades têm restrições adicionais de eliminação. Os cartões Jira apenas de leitura também impedem alterações ou eliminações que os removam.',
    ],
  },
  {
    id: 'jira-connect',
    category: 'Jira synchronization',
    en: [
      'How do I connect a project to Jira?',
      'As workspace owner, open the project’s three-dot Project options → Jira synchronization. Each project supports one connection. Choose Data Center / Server for a personal access token (PAT), or Cloud for an account email and API token. Enter the HTTPS base URL, project key for new issues and JQL. Retain any Data Center context path; classic Cloud URLs look like https://your-site.atlassian.net, while scoped tokens use https://api.atlassian.com/ex/jira/YOUR_CLOUD_ID. Approve server access if needed, then Test connection and load mappings. Select the issue type, map fields, choose direction and schedule, enable and save.',
    ],
    pt: [
      'Como ligo um projeto ao Jira?',
      'Como proprietário do espaço, abra o menu de três pontos Opções do projeto → Sincronização Jira. Cada projeto admite uma ligação. Escolha Data Center / Server para um token de acesso pessoal (PAT), ou Cloud para email da conta e token de API. Introduza o URL base HTTPS, a chave do projeto para novos pedidos e a JQL. Mantenha o caminho de contexto do Data Center; os URLs Cloud clássicos seguem https://your-site.atlassian.net, enquanto os tokens com âmbito usam https://api.atlassian.com/ex/jira/YOUR_CLOUD_ID. Aprove o acesso ao servidor, se necessário, e teste a ligação para carregar os mapeamentos. Selecione o tipo de pedido, mapeie campos, escolha direção e horário, ative e guarde.',
    ],
  },
  {
    id: 'jira-server',
    category: 'Jira synchronization',
    en: [
      'How do I approve a Jira server without changing deployment settings?',
      'In Jira synchronization, enter the HTTPS URL and expand Server access. A platform administrator chooses Approve this Jira server and confirms the exact hostname and any non-default port. Workspace owners without that permission ask a platform administrator to use the same panel. Approval is saved immediately: no environment variables or container restarts are needed. Standard atlassian.net Cloud sites are already allowed. Administrators can revoke saved approvals; deployment-policy allowances are read-only here. Approve only trusted servers because the connector sends credentials to them.',
    ],
    pt: [
      'Como aprovo um servidor Jira sem alterar a instalação?',
      'Em Sincronização Jira, introduza o URL HTTPS e abra Acesso ao servidor. Um administrador da plataforma escolhe Aprovar este servidor Jira e confirma o nome do servidor e qualquer porta não predefinida. Os proprietários sem essa permissão pedem a um administrador que use o mesmo painel. A aprovação é guardada imediatamente: não são necessárias variáveis de ambiente nem reinícios de contentores. Os sites Cloud atlassian.net padrão já são permitidos. Os administradores podem revogar aprovações guardadas; as permissões impostas pela instalação são apenas de leitura neste painel. Aprove apenas servidores de confiança, pois recebem as credenciais do conector.',
    ],
  },
  {
    id: 'jira-direction',
    category: 'Jira synchronization',
    en: [
      'What do the three synchronization directions do?',
      'Jira to Domus imports matching issues and updates linked cards without writing to Jira. Domus to Jira creates issues for unlinked project cards and updates linked issues within the JQL scope, without importing unrelated Jira issues. Bidirectional creates missing counterparts and propagates changes from either side. If both sides changed, the original creation system wins for synchronized fields: Jira for imports, Domus for exports. Cards are not matched by title. Assignees, when enabled, always flow only from Jira. Deletions never propagate in either direction.',
    ],
    pt: [
      'O que fazem as três direções de sincronização?',
      'Jira para Domus importa pedidos correspondentes e atualiza cartões ligados sem escrever no Jira. Domus para Jira cria pedidos para cartões do projeto ainda sem ligação e atualiza pedidos ligados abrangidos pela JQL, sem importar outros pedidos Jira. Bidirecional cria os elementos em falta e transmite alterações de ambos os lados. Se ambos mudarem, o sistema de criação original prevalece nos campos sincronizados: Jira nas importações, Domus nas exportações. Os cartões não são associados pelo título. Os responsáveis, quando ativados, vêm sempre apenas do Jira. As eliminações nunca são propagadas em qualquer direção.',
    ],
  },
  {
    id: 'jira-scope',
    category: 'Jira synchronization',
    en: [
      'How does JQL limit synchronization, and which fields are included?',
      'JQL is evaluated on every run, including for already linked issues. Start with a narrow query such as project = TEAM ORDER BY updated DESC and test it. Ensure the query also includes issues created in your chosen outbound project. Titles, descriptions, statuses, priorities, labels and due dates synchronize; assignees are optional and inbound only. Comments, attachments, checklists, buckets and swimlanes do not synchronize. Cloud descriptions are represented as plain text, not a full rich-format round trip. Issues outside the query or no longer accessible are left alone and reported. More than 10,000 results is an error, not a partial import.',
    ],
    pt: [
      'Como limita a JQL a sincronização e que campos são incluídos?',
      'A JQL é avaliada em cada execução, incluindo os pedidos já ligados. Comece com uma consulta restrita, como project = TEAM ORDER BY updated DESC, e teste-a. Garanta que também inclui pedidos criados no projeto de destino escolhido. Sincronizam-se títulos, descrições, estados, prioridades, etiquetas e datas limite; os responsáveis são opcionais e apenas de entrada. Comentários, anexos, listas de verificação, grupos e faixas não são sincronizados. As descrições Cloud são representadas como texto simples, sem preservar toda a formatação numa ida e volta. Os pedidos fora da consulta ou inacessíveis não são alterados e são reportados. Mais de 10 000 resultados gera um erro, não uma importação parcial.',
    ],
  },
  {
    id: 'jira-status-mapping',
    category: 'Jira synchronization',
    en: [
      'How do I map several Jira statuses to one Domus status?',
      'Test connection and load mappings populates dropdowns with Jira names; saved connections load them automatically. Under Status mapping, choose the matching Jira status and use Add Jira status for additional states that should share a Domus status. Each Jira status belongs to one Domus status. Select exactly one Default outbound target per mapped Domus status. If Jira is already in any status mapped to the desired Domus status, no transition is made. Map priorities separately: they remain one-to-one. Unavailable choices or unmapped values need correction, not a guessed replacement.',
    ],
    pt: [
      'Como associo vários estados Jira a um estado Domus?',
      'Testar a ligação e carregar mapeamentos preenche os seletores com nomes Jira; as ligações guardadas carregam-nos automaticamente. Em Mapeamento de estados, escolha o estado correspondente e use Adicionar estado Jira para outros estados que devam partilhar o mesmo estado Domus. Cada estado Jira pertence a um estado Domus. Selecione exatamente um destino predefinido de saída por estado Domus mapeado. Se o Jira já estiver num estado associado ao estado Domus pretendido, não é feita qualquer transição. Mapeie as prioridades separadamente: continuam a ser um-para-um. As opções indisponíveis e os valores sem mapeamento precisam de correção, não de uma substituição presumida.',
    ],
  },
  {
    id: 'jira-assignees',
    category: 'Jira synchronization',
    en: [
      'How do I map Jira assignees to workspace members?',
      'Enable Synchronize assignees from Jira under Assignee mapping, then select Jira users and registered workspace members by name. Invitees must join first. Choices include users found in the JQL results and saved mappings, not the entire Jira directory. This option is off by default and works only with Jira to Domus or Bidirectional. The mapped member replaces all current card assignees. An unassigned Jira issue clears them; an unmapped Jira user also clears them and records an Assignee mapping warning. Fix the mapping and run again. Domus never writes Jira assignees, even when creating issues.',
    ],
    pt: [
      'Como associo responsáveis Jira a membros do espaço?',
      'Ative Sincronizar responsáveis do Jira em Mapeamento de responsáveis e selecione utilizadores Jira e membros registados pelo nome. Os convidados têm de aderir primeiro. As opções incluem utilizadores encontrados nos resultados JQL e nos mapeamentos guardados, não todo o diretório Jira. A opção está desativada por predefinição e só funciona com Jira para Domus ou Bidirecional. O membro mapeado substitui todos os responsáveis atuais do cartão. Um pedido sem responsável limpa as atribuições; um utilizador Jira sem mapeamento também as limpa e regista um aviso. Corrija o mapeamento e execute novamente. O Domus nunca escreve responsáveis no Jira, nem ao criar pedidos.',
    ],
  },
  {
    id: 'jira-schedule',
    category: 'Jira synchronization',
    en: [
      'How do I schedule synchronization or run it immediately?',
      'Choose an interval and Minutes, Hours, Days or Months, apply the preset, and review the five-field cron expression and time zone. For example, */15 * * * * runs every 15 minutes; 0 9 * * * runs daily at 09:00 in the selected zone. Cron follows calendar boundaries: every two days is not always an exact 48-hour interval, and nonexistent dates are skipped. Enable synchronization and save. Run saved configuration now uses the saved settings, not unsaved edits; use Refresh status to check progress. The worker runs independently of your browser. Timing is approximate, and missed schedules during downtime coalesce into one run.',
    ],
    pt: [
      'Como agendo a sincronização ou a executo imediatamente?',
      'Escolha um intervalo e Minutos, Horas, Dias ou Meses, aplique a predefinição e reveja a expressão cron de cinco campos e o fuso horário. Por exemplo, */15 * * * * executa a cada 15 minutos; 0 9 * * * executa diariamente às 09:00 no fuso escolhido. O cron segue o calendário: de dois em dois dias nem sempre significa exatamente 48 horas e as datas inexistentes são ignoradas. Ative a sincronização e guarde. Executar configuração guardada agora usa as definições guardadas, não as edições pendentes; use Atualizar estado para acompanhar. O processo de sincronização funciona sem o navegador aberto. O horário é aproximado e os agendamentos perdidos durante uma paragem são reunidos numa execução.',
    ],
  },
  {
    id: 'jira-read-only',
    category: 'Jira synchronization',
    en: [
      'Why is my Jira card locked, and how do I open it in Jira?',
      'A card with a confirmed Jira link is entirely read-only when its connection is Jira to Domus. Fields, assignments, comments, checklists, attachment changes, dragging, duplication and deletion are disabled. History, downloads and Open in Jira remain available in Details; the Jira link opens a new tab and Jira may require its own sign-in. Unlinked cards remain editable. Pausing synchronization does not unlock linked cards: the workspace owner must change direction to allow local edits. A workspace containing locked cards cannot be deleted.',
    ],
    pt: [
      'Porque está bloqueado o meu cartão Jira e como o abro no Jira?',
      'Um cartão com ligação Jira confirmada é inteiramente de leitura quando a direção é Jira para Domus. Campos, atribuições, comentários, listas de verificação, alterações de anexos, arrastar, duplicar e eliminar ficam desativados. O Histórico, as transferências e Abrir no Jira continuam disponíveis em Detalhes; a ligação abre um novo separador e o Jira pode exigir início de sessão próprio. Os cartões sem ligação continuam editáveis. Pausar a sincronização não desbloqueia os cartões ligados: o proprietário tem de mudar a direção para permitir edições locais. Não é possível eliminar um espaço que contenha cartões bloqueados.',
    ],
  },
  {
    id: 'jira-troubleshooting',
    category: 'Jira synchronization',
    en: [
      'What should I do when a Jira run fails or reports warnings?',
      'Open Jira synchronization and Refresh status, then inspect the run and affected issue details. Test connection and load mappings to check the URL, credentials and JQL without writing issues. For 401/403, check token validity and permissions; for 404, check the base URL, context path, Cloud token URL and issue visibility. Fix missing status/priority/assignee mappings, required Jira fields or unavailable transitions. Required custom fields are not supplied by this connector. Respect rate-limit errors and retry later. Save corrections before running again; an item can fail while others succeed. Never share tokens in screenshots or error reports.',
    ],
    pt: [
      'O que faço quando uma execução Jira falha ou apresenta avisos?',
      'Abra Sincronização Jira e Atualizar estado; consulte a execução e os detalhes dos pedidos afetados. Teste a ligação e carregue mapeamentos para verificar URL, credenciais e JQL sem escrever pedidos. Para 401/403, verifique a validade e permissões do token; para 404, confirme o URL base, o caminho de contexto, o URL do token Cloud e a visibilidade do pedido. Corrija mapeamentos de estados/prioridades/responsáveis, campos obrigatórios Jira ou transições indisponíveis. Este conector não fornece campos personalizados obrigatórios. Respeite os limites de pedidos e tente mais tarde. Guarde as correções antes de executar novamente; um item pode falhar enquanto outros têm sucesso. Nunca partilhe tokens em capturas ou relatórios de erro.',
    ],
  },
  {
    id: 'jira-pause',
    category: 'Jira synchronization',
    en: [
      'How do I pause Jira synchronization or change its configuration safely?',
      'Turn off Enable synchronization and save; let any running job finish before making major changes. Archiving a project pauses scheduled and manual runs. Deleting a project removes its connection and links, not the Jira issues, but deletion is blocked if it would remove read-only cards. A blank token preserves the saved credential. Once issue links exist, the Jira instance, edition and target project cannot be changed on that connection. Adjust JQL, mappings or direction carefully: origin-based conflict handling remains unchanged, and pausing alone does not remove card locks or Open in Jira links.',
    ],
    pt: [
      'Como pauso a sincronização Jira ou altero a configuração em segurança?',
      'Desative Ativar sincronização e guarde; deixe terminar qualquer execução em curso antes de alterações importantes. Arquivar um projeto pausa execuções agendadas e manuais. Eliminar um projeto remove a ligação e associações, não os pedidos Jira, mas a eliminação é impedida se remover cartões apenas de leitura. Um token em branco preserva a credencial guardada. Depois de existirem ligações a pedidos, não pode mudar a instância Jira, edição ou projeto de destino nessa ligação. Altere JQL, mapeamentos ou direção com cuidado: a resolução de conflitos pela origem mantém-se e pausar não remove bloqueios nem ligações Abrir no Jira.',
    ],
  },
  {
    id: 'assistant',
    category: 'AI & assistants',
    en: [
      'Where is the assistant and how do I send a question?',
      'Use the sparkle button in the bottom-right corner on any platform page. The assistant opens with the current project or automation area. Switch between Projects and Automations in the window, and choose a project when needed. Enter or Send submits the question; Shift+Enter adds a line. Your question appears immediately while the assistant thinks. Cancel stops the request and restores the text; New conversation clears the current exchange. Close or Escape returns to the page.',
    ],
    pt: [
      'Onde está o assistente e como envio uma pergunta?',
      'Use o botão com brilhos no canto inferior direito de qualquer página da plataforma. O assistente abre com o projeto atual ou a área de automações. Alterne entre Projetos e Automações na janela e escolha um projeto quando necessário. Enter ou Enviar submete a pergunta; Shift+Enter acrescenta uma linha. A pergunta aparece logo na conversa enquanto o assistente pensa. Cancelar interrompe o pedido e repõe o texto; Nova conversa limpa a troca atual. Fechar ou Escape regressa à página.',
    ],
  },
  {
    id: 'ai-connection',
    category: 'AI & assistants',
    en: [
      'How do I connect my AI subscription?',
      'Open Settings → Workspace → AI settings, or AI settings from the assistant when setup is needed. Connect ChatGPT using subscription sign-in, choose a model and save it. Each member connects their own account; connections are not shared. The workspace owner enables AI for the workspace. Domus asks for no API key and sets no monthly spending limit; your subscription provider’s usage limits apply. Subscription sign-in must be configured by the installation administrator. Claude and Gemini subscription connections are not currently available.',
    ],
    pt: [
      'Como ligo a minha subscrição de IA?',
      'Abra Definições → Espaço de trabalho → Definições de IA, ou as Definições de IA no assistente quando falta configurar. Ligue o ChatGPT pelo início de sessão da subscrição, escolha um modelo e guarde. Cada membro liga a própria conta; as ligações não são partilhadas. O proprietário ativa a IA no espaço. O Domus não pede chave de API nem define um limite mensal de despesa; aplicam-se os limites do fornecedor da subscrição. O administrador da instalação tem de configurar o início de sessão da subscrição. As ligações por subscrição Claude e Gemini ainda não estão disponíveis.',
    ],
  },
  {
    id: 'ai-context',
    category: 'AI & assistants',
    en: [
      'Can the assistant find all cards, logs and results?',
      'The assistant searches the selected project and reads workspace automation data on demand. It can retrieve complete card fields, comments, checklist, attachments and history, as well as automation manifests, execution and installation history, retained logs and result files, through authenticated MCP tools. Data arrives in pages or chunks; Domus does not send every card to the model at once. A question has a bounded research budget, so an answer may be partial and should say so. Select the right project, refine the question and check source cards. Access follows your current permissions. Temporary secret execution inputs are deleted; discarded log output cannot be recovered.',
    ],
    pt: [
      'O assistente encontra todos os cartões, registos e resultados?',
      'O assistente pesquisa o projeto selecionado e consulta as automações do espaço quando precisa. Pode obter campos completos dos cartões, comentários, listas de verificação, anexos e histórico, bem como manifestos de automações, histórico de execuções e instalações, registos guardados e ficheiros de resultado, através de ferramentas MCP autenticadas. Os dados chegam em páginas ou partes; o Domus não envia todos os cartões ao modelo de uma vez. Cada pergunta tem um limite de pesquisa, pelo que uma resposta pode ser parcial e deve indicá-lo. Escolha o projeto correto, refine a pergunta e consulte os cartões de origem. O acesso segue as suas permissões atuais. Os dados secretos temporários das execuções são eliminados; a saída dos registos já descartada não pode ser recuperada.',
    ],
  },
  {
    id: 'ai-suggestions',
    category: 'AI & assistants',
    en: [
      'How do I review AI suggestions for a card?',
      'Save the card before requesting assistance. In the card’s AI controls choose Improve description, Summarize card or Generate checklist, then Generate suggestion. Review the result. Apply to draft updates only the editable draft; Save task keeps it. Dismiss ignores it. Existing checklist items are not duplicated. If the card changes, generate a fresh suggestion. The chat assistant answers questions but does not install automations, start runs or save card changes for you.',
    ],
    pt: [
      'Como revejo sugestões de IA para um cartão?',
      'Guarde o cartão antes de pedir assistência. Nos controlos de IA do cartão, escolha Melhorar descrição, Resumir cartão ou Gerar checklist e depois Gerar sugestão. Reveja o resultado. Aplicar ao rascunho altera apenas o rascunho editável; Guardar tarefa conserva a alteração. Dispensar ignora-a. Os itens existentes da lista não são duplicados. Se o cartão mudar, gere uma nova sugestão. O assistente de conversa responde a perguntas, mas não instala automações, inicia execuções nem guarda alterações nos cartões por si.',
    ],
  },
  {
    id: 'ai-troubleshooting',
    category: 'AI & assistants',
    en: [
      'Why is AI unavailable or a question incomplete?',
      'Check that your ChatGPT account is connected, a model is saved and AI is enabled for this workspace. If subscription sign-in is unavailable, contact the installation administrator. Reconnect an expired connection or choose a model available to your plan. After an error or cancellation the question is restored for retry. For incomplete answers, narrow the question or choose the relevant project. New conversation starts fresh; provider limits may require waiting. Review source records before acting on an answer.',
    ],
    pt: [
      'Porque está a IA indisponível ou uma resposta incompleta?',
      'Confirme que a sua conta ChatGPT está ligada, existe um modelo guardado e a IA está ativa neste espaço. Se o início de sessão da subscrição não estiver disponível, contacte o administrador da instalação. Volte a ligar uma ligação expirada ou escolha um modelo disponível no seu plano. Depois de um erro ou cancelamento, a pergunta é reposta para repetir. Para respostas incompletas, restrinja a pergunta ou escolha o projeto relevante. Nova conversa começa de novo; os limites do fornecedor podem exigir esperar. Reveja os registos de origem antes de agir sobre uma resposta.',
    ],
  },
  {
    id: 'automation-catalog',
    category: 'Automations',
    en: [
      'How do I install several automations at once?',
      'Open Automations → Catalog, search by name or description and choose Install or Update on each desired row. You can queue several installations without waiting for the first to finish. Each row shows its own progress or failure. The workspace owner manages installations. Refresh catalog to check available versions; a platform administrator configures the source repository in automation settings.',
    ],
    pt: [
      'Como instalo várias automações em simultâneo?',
      'Abra Automações → Catálogo, pesquise pelo nome ou descrição e escolha Instalar ou Atualizar em cada linha pretendida. Pode colocar várias instalações na fila sem esperar que a primeira termine. Cada linha mostra o próprio progresso ou erro. O proprietário do espaço gere as instalações. Atualizar catálogo consulta as versões disponíveis; um administrador da plataforma configura o repositório de origem nas definições de automações.',
    ],
  },
  {
    id: 'automation-run',
    category: 'Automations',
    en: [
      'How do I execute or cancel an automation?',
      'Open Automations → Flows and choose an installed compatible automation. Fill its required inputs, review the effects and start the execution. Executions shows status; View execution opens details. Queued means waiting, not completed. Cancel execution requests cancellation while the run is active and does not undo effects already performed. The assistant can explain installed automations and failures; starting or cancelling still uses these controls.',
    ],
    pt: [
      'Como executo ou cancelo uma automação?',
      'Abra Automações → Fluxos e escolha uma automação instalada e compatível. Preencha os dados obrigatórios, reveja os efeitos e inicie a execução. Execuções mostra o estado; Ver execução abre os detalhes. Na fila significa à espera, não concluída. Cancelar execução pede a interrupção enquanto está ativa e não desfaz efeitos já realizados. O assistente pode explicar automações instaladas e erros; iniciar ou cancelar continua a fazer-se nestes controlos.',
    ],
  },
  {
    id: 'automation-results',
    category: 'Automations',
    en: [
      'Where are execution logs and ZIP results?',
      'Open Automations → Executions → View execution. Expand Execution log to inspect the retained output. When a finished run has result files, use Download ZIP results. Failed or cancelled runs can also have useful results. The assistant can inspect authorized retained logs and result files; it cannot reconstruct deleted inputs or discarded output.',
    ],
    pt: [
      'Onde vejo os registos de execução e resultados ZIP?',
      'Abra Automações → Execuções → Ver execução. Expanda Registo da execução para consultar a saída guardada. Quando uma execução terminada tem ficheiros de resultado, use Descarregar resultados ZIP. As execuções com erro ou canceladas também podem ter resultados úteis. O assistente pode consultar registos guardados e resultados autorizados; não consegue reconstruir dados de entrada eliminados ou saída descartada.',
    ],
  },
  {
    id: 'automation-settings',
    category: 'Automations',
    en: [
      'Who configures the automation catalog and repository?',
      'A platform administrator opens Settings → Automations to configure the GitHub repository and its access credential when needed. The credential is stored protected and is never shown to the AI assistant. Workspace owners install, update or remove automation packages. Workspace members can run installed automations with valid inputs. Every request and MCP read uses your current session and workspace permissions.',
    ],
    pt: [
      'Quem configura o catálogo e o repositório das automações?',
      'Um administrador da plataforma abre Definições → Automações para configurar o repositório GitHub e a credencial de acesso quando necessária. A credencial fica protegida e nunca é mostrada ao assistente de IA. Os proprietários dos espaços instalam, atualizam ou removem os pacotes. Os membros podem executar automações instaladas com dados válidos. Cada pedido e consulta MCP usa a sua sessão e as permissões do espaço.',
    ],
  },
];

import { useI18n } from '../../shared/i18n';
import {
  FolderKanban,
  BriefcaseBusiness,
  CalendarDays,
  ChartNoAxesCombined,
  ClipboardList,
  Compass,
  FileCheck2,
  Layers3,
  Network,
  NotebookPen,
  Package,
  ShieldCheck,
  Target,
  UsersRound,
  type LucideIcon,
} from 'lucide-react';
export const projectIcons: { id: string; label: string; icon: LucideIcon }[] = [
  { id: 'folder', label: 'Project', icon: FolderKanban },
  { id: 'notebook', label: 'Notes', icon: NotebookPen },
  { id: 'briefcase', label: 'Business', icon: BriefcaseBusiness },
  { id: 'calendar', label: 'Calendar', icon: CalendarDays },
  { id: 'chart', label: 'Dashboard', icon: ChartNoAxesCombined },
  { id: 'checklist', label: 'Checklist', icon: ClipboardList },
  { id: 'compass', label: 'Planning', icon: Compass },
  { id: 'check', label: 'Delivery', icon: FileCheck2 },
  { id: 'layers', label: 'Design', icon: Layers3 },
  { id: 'network', label: 'Infrastructure', icon: Network },
  { id: 'package', label: 'Product', icon: Package },
  { id: 'shield', label: 'Security', icon: ShieldCheck },
  { id: 'target', label: 'Goals', icon: Target },
  { id: 'users', label: 'Team', icon: UsersRound },
];
export function ProjectIcon({ name, size = 18 }: { name?: string; size?: number }) {
  const Icon = projectIcons.find((item) => item.id === name)?.icon ?? FolderKanban;
  return <Icon size={size} aria-hidden="true" />;
}

export function ProjectIconPicker({ value = 'folder' }: { value?: string }) {
  const { t } = useI18n();
  return (
    <fieldset className="project-icon-picker">
      <legend>{t('Project icon')}</legend>
      <div className="project-icon-grid">
        {projectIcons.map(({ id, label, icon: Icon }) => (
          <label key={id} title={t(label)}>
            <input
              type="radio"
              name="icon"
              value={id}
              defaultChecked={id === value}
              aria-label={t(label)}
            />
            <span>
              <Icon size={21} aria-hidden="true" />
            </span>
          </label>
        ))}
      </div>
    </fieldset>
  );
}

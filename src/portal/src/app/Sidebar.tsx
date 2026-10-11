import { ProjectIcon } from '../features/projects/ProjectIcon';
import { Brand } from '../shared/Brand';
import {
  Bot,
  LibraryBig,
  History,
  ListTodo,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  CircleHelp,
  BriefcaseBusiness,
  FolderKanban,
  PanelsTopLeft,
  PlusCircle,
  SlidersHorizontal,
  UsersRound,
  Building2,
  Cable,
  Waypoints,
  NotebookPen,
  LayoutDashboard,
  CalendarDays,
  ScrollText,
  Landmark,
  FileCheck2,
  ShieldCheck,
  BookMarked,
  ChartNoAxesCombined,
  Network,
  Layers3,
  ClipboardList,
  SearchCheck,
  FolderLock,
  FileClock,
  FileSignature,
  Target,
  Package,
  Compass,
  Archive,
  type LucideIcon,
} from 'lucide-react';
import React, { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import type { Member } from '../domain/models';
import { type Project, type State, type Task } from '../domain/models';
import { isActivitiesProject } from '../domain/projectRules';
import { LogoutButton } from '../features/auth/AuthBoundary';

type SidebarProps = {
  canManagePlatform: boolean;
  sidebar: boolean;
  navigate: (name: string) => void;
  t: (value: unknown, ...values: unknown[]) => string;
  sidebarWidth: number;
  setSidebarWidth: React.Dispatch<React.SetStateAction<number>>;
  sidebarCollapsed: boolean;
  setSidebarCollapsed: React.Dispatch<React.SetStateAction<boolean>>;
  data: State;
  setModal: React.Dispatch<React.SetStateAction<string | null>>;
  page: string;
  activeTasks: Task[];
  currentMember: Member;
  isDone: (task: Task) => boolean;
  project: Project;
  setProjectId: React.Dispatch<React.SetStateAction<string>>;
};

export function Sidebar({
  canManagePlatform,
  sidebar,
  navigate,
  t,
  sidebarWidth,
  setSidebarWidth,
  sidebarCollapsed,
  setSidebarCollapsed,
  data,
  setModal,
  page,
  activeTasks,
  currentMember,
  isDone,
  project,
  setProjectId,
}: SidebarProps) {
  const sidebarElement = useRef<HTMLElement>(null);
  const drag = useRef<{ x: number; width: number; latest: number } | null>(null);
  const [maximumWidth, setMaximumWidth] = useState(420);
  useEffect(() => {
    const measure = () => {
      const canvas = document.createElement('canvas');
      const context = canvas.getContext('2d');
      if (!context) return;
      let longest = 0;
      sidebarElement.current
        ?.querySelectorAll<HTMLElement>(
          '.nav-text, .project-nav-text, .workspace > span:nth-child(2)',
        )
        .forEach((label) => {
          context.font = getComputedStyle(label).font;
          longest = Math.max(longest, context.measureText(label.textContent ?? '').width);
        });
      const maximum = Math.max(236, Math.min(Math.ceil(longest + 140), window.innerWidth - 320));
      setMaximumWidth(maximum);
      setSidebarWidth((width) => Math.max(180, Math.min(width, maximum)));
    };
    measure();
    void document.fonts.ready.then(measure);
    window.addEventListener('resize', measure);
    return () => window.removeEventListener('resize', measure);
  }, [data.projects, data.workspace.name, t, setSidebarWidth]);
  useEffect(() => () => document.documentElement.classList.remove('sidebar-resizing'), []);
  const resize = (requested: number, persist = false) => {
    const width = Math.max(76, Math.min(requested, maximumWidth));
    const collapsed = width < 180;
    setSidebarCollapsed(collapsed);
    if (!collapsed) setSidebarWidth(width);
    if (persist) {
      try {
        localStorage.setItem('domus-sidebar-collapsed', String(collapsed));
        if (!collapsed) localStorage.setItem('domus-sidebar-width', String(width));
      } catch {
        /* Keep the current session preference. */
      }
    }
  };
  const finishResize = () => {
    if (drag.current) resize(drag.current.latest, true);
    drag.current = null;
    document.documentElement.classList.remove('sidebar-resizing');
  };
  const [groups, setGroups] = useState({
    management: false,
    projects: false,
    automations: false,
    settings: false,
  });
  const toggle = (group: keyof typeof groups) =>
    setGroups((current) => ({
      management: group === 'projects' || (group === 'management' && !current.management),
      projects: group === 'projects' && !current.projects,
      automations: group === 'automations' && !current.automations,
      settings: group === 'settings' && !current.settings,
    }));
  const groupButton = (group: keyof typeof groups, label: string, Icon: LucideIcon) => (
    <button
      className="nav-group-toggle"
      aria-label={t(label)}
      aria-expanded={groups[group]}
      aria-controls={`domus-nav-${group}`}
      onClick={() => toggle(group)}
    >
      <Icon size={18} />
      <span className="nav-text">{t(label)}</span>
      <ChevronDown size={14} className={`nav-group-chevron ${groups[group] ? 'expanded' : ''}`} />
    </button>
  );
  const navigationItem = (label: string, target: string, Icon: LucideIcon) => (
    <button
      aria-label={t(label)}
      title={sidebarCollapsed ? undefined : t(label)}
      className={page === target ? 'active' : ''}
      onClick={() => navigate(target)}
    >
      <Icon size={18} />
      <span className="nav-text">{t(label)}</span>
      {target === 'My tasks' && (
        <span className="nav-count">
          {
            activeTasks.filter(
              (task) => task.assignees.includes(currentMember.name) && !isDone(task),
            ).length
          }
        </span>
      )}
    </button>
  );
  const [tooltip, setTooltip] = useState<{ label: string; left: number; top: number } | null>(null);
  const showTooltip = (event: React.MouseEvent<HTMLElement> | React.FocusEvent<HTMLElement>) => {
    if (!sidebarCollapsed || !window.matchMedia('(min-width: 761px)').matches) return;
    const button = (event.target as Element).closest('button');
    const label = button?.getAttribute('aria-label');
    if (!button || !label) {
      setTooltip(null);
      return;
    }
    const rect = button.getBoundingClientRect();
    setTooltip({
      label,
      left: event.currentTarget.getBoundingClientRect().right + 12,
      top: Math.max(24, Math.min(window.innerHeight - 24, rect.top + rect.height / 2)),
    });
  };
  useEffect(() => {
    const dismiss = () => setTooltip(null);
    window.addEventListener('resize', dismiss);
    window.addEventListener('scroll', dismiss, true);
    return () => {
      window.removeEventListener('resize', dismiss);
      window.removeEventListener('scroll', dismiss, true);
    };
  }, []);
  return (
    <aside
      ref={sidebarElement}
      className={`sidebar ${sidebar ? 'open' : ''}`}
      onMouseOver={showTooltip}
      onMouseLeave={() => setTooltip(null)}
      onFocus={showTooltip}
      onBlur={() => setTooltip(null)}
      onClick={() => setTooltip(null)}
      onKeyDown={(event) => {
        if (event.key === 'Escape') setTooltip(null);
      }}
    >
      {sidebarCollapsed &&
        tooltip &&
        createPortal(
          <div
            className="sidebar-tooltip"
            role="tooltip"
            style={{ left: tooltip.left, top: tooltip.top }}
          >
            {tooltip.label}
          </div>,
          document.body,
        )}
      <button
        className="sidebar-collapse-toggle"
        aria-label={sidebarCollapsed ? t('Expand sidebar') : t('Collapse sidebar')}
        title={sidebarCollapsed ? undefined : t('Collapse sidebar')}
        aria-expanded={!sidebarCollapsed}
        onClick={() => {
          const next = !sidebarCollapsed;
          setSidebarCollapsed(next);
          try {
            localStorage.setItem('domus-sidebar-collapsed', String(next));
          } catch {
            /* Keep the current session preference. */
          }
        }}
      >
        {sidebarCollapsed ? <ChevronRight size={15} /> : <ChevronLeft size={15} />}
      </button>
      <div
        className="sidebar-resize-handle"
        role="separator"
        aria-label={t('Resize sidebar')}
        aria-orientation="vertical"
        aria-valuemin={76}
        aria-valuemax={maximumWidth}
        aria-valuenow={sidebarCollapsed ? 76 : Math.round(sidebarWidth)}
        tabIndex={0}
        onPointerDown={(event) => {
          if (event.button !== 0) return;
          event.preventDefault();
          const width = sidebarCollapsed ? 76 : sidebarWidth;
          drag.current = { x: event.clientX, width, latest: width };
          event.currentTarget.setPointerCapture(event.pointerId);
          document.documentElement.classList.add('sidebar-resizing');
        }}
        onPointerMove={(event) => {
          if (!drag.current) return;
          drag.current.latest = drag.current.width + event.clientX - drag.current.x;
          resize(drag.current.latest);
        }}
        onPointerUp={finishResize}
        onPointerCancel={finishResize}
        onLostPointerCapture={finishResize}
        onDoubleClick={() => resize(maximumWidth, true)}
        onKeyDown={(event) => {
          const width = sidebarCollapsed ? 76 : sidebarWidth;
          const next =
            event.key === 'ArrowRight'
              ? sidebarCollapsed
                ? 180
                : width + 16
              : event.key === 'ArrowLeft'
                ? width - 16
                : event.key === 'Home'
                  ? 76
                  : event.key === 'End'
                    ? maximumWidth
                    : null;
          if (next === null) return;
          event.preventDefault();
          resize(next, true);
        }}
      />
      <div className="sidebar-scroll">
        <a
          className="logo"
          href="#"
          onClick={(e) => {
            e.preventDefault();
            navigate('Overview');
          }}
        >
          <Brand sidebar />
        </a>
        <button
          aria-label={t('Switch workspace')}
          title={sidebarCollapsed ? undefined : data.workspace.name}
          className="workspace"
          onClick={() => setModal('Workspace')}
        >
          <span className="workspace-icon">
            {data.workspace.icon ? (
              <img src={data.workspace.icon} alt={t('Workspace icon')} />
            ) : (
              <>
                {data.workspace.name[0].toUpperCase()}
                <span>✳</span>
              </>
            )}
          </span>
          <span>
            {data.workspace.name}
            <small>
              {data.members.length} {t('members')}
            </small>
          </span>
          <ChevronDown size={15} />
        </button>
        <div className="nav-label">{t('WORKSPACE')}</div>
        <nav aria-label={t('Workspace navigation')}>
          {navigationItem('Overview', 'Overview', LayoutDashboard)}
          {groupButton('management', 'Management', BriefcaseBusiness)}
          <div id="domus-nav-management" className="nav-group-children" hidden={!groups.management}>
            {navigationItem('My tasks', 'My tasks', ListTodo)}
            {navigationItem('Projects', 'Project directory', FolderKanban)}
            {navigationItem('Members', 'Members', UsersRound)}
            {groupButton('projects', 'Boards', PanelsTopLeft)}
            <div
              id="domus-nav-projects"
              className="nav-group-children project-nav"
              hidden={!groups.projects}
            >
              {[...data.projects.filter((item) => !item.archived)]
                .sort(
                  (left, right) =>
                    Number(isActivitiesProject(right)) - Number(isActivitiesProject(left)),
                )
                .map((item, index) => (
                  <button
                    key={item.id}
                    aria-label={isActivitiesProject(item) ? t('My activities') : item.name}
                    title={sidebarCollapsed ? undefined : item.name}
                    className={project.id === item.id && page === 'Projects' ? 'selected' : ''}
                    onClick={() => {
                      setProjectId(item.id);
                      navigate('Projects');
                    }}
                  >
                    {item.icon ? (
                      <ProjectIcon name={item.icon} size={16} />
                    ) : (
                      <BoardIcon index={index} activities={isActivitiesProject(item)} />
                    )}
                    <span className="project-nav-text">
                      {isActivitiesProject(item) ? t('My activities') : item.name}
                    </span>
                  </button>
                ))}
              <button aria-label={t('New project')} onClick={() => setModal('New project')}>
                <PlusCircle size={16} />
                <span className="nav-text">{t('New project')}</span>
              </button>
            </div>
          </div>
          {groupButton('automations', 'Automations', Bot)}
          <div
            id="domus-nav-automations"
            className="nav-group-children"
            hidden={!groups.automations}
          >
            {navigationItem('Flows', 'Automations', Waypoints)}
            {navigationItem('Catalog', 'Catalog', LibraryBig)}
            {navigationItem('Executions', 'Executions', History)}
          </div>
          {groupButton('settings', 'Settings', SlidersHorizontal)}
          <div id="domus-nav-settings" className="nav-group-children" hidden={!groups.settings}>
            <button aria-label={t('Workspace')} onClick={() => setModal('Settings')}>
              <Building2 size={18} />
              <span className="nav-text">{t('Workspace')}</span>
            </button>
            {canManagePlatform && navigationItem('Automations', 'Automation settings', Cable)}
          </div>
        </nav>
        <div className="sidebar-bottom">
          <button
            aria-label={t('Help')}
            title={sidebarCollapsed ? undefined : t('Help')}
            onClick={() => navigate('Help')}
          >
            <CircleHelp size={17} />
            <span className="nav-text">{t('Help')}</span>
          </button>
          <LogoutButton />
        </div>
      </div>
    </aside>
  );
}

const boardIcons: LucideIcon[] = [
  CalendarDays,
  ScrollText,
  Landmark,
  FileCheck2,
  ShieldCheck,
  BookMarked,
  ChartNoAxesCombined,
  Network,
  Layers3,
  ClipboardList,
  SearchCheck,
  FolderLock,
  FileClock,
  FileSignature,
  Target,
  Package,
  Compass,
  Archive,
];
function BoardIcon({ index, activities }: { index: number; activities: boolean }) {
  if (activities) return <NotebookPen size={16} />;
  const Icon = boardIcons[index];
  if (Icon) return <Icon size={16} />;
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" aria-hidden="true">
      <rect
        x="2"
        y="2"
        width="20"
        height="20"
        rx="4"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.5"
      />
      <text x="12" y="16" textAnchor="middle" fontSize="10" fill="currentColor">
        {index + 1}
      </text>
    </svg>
  );
}

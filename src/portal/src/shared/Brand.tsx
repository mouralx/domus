import { useBranding } from './PlatformBranding';
export function Brand({ sidebar = false }: { sidebar?: boolean }) {
  const { branding } = useBranding();
  const logoOnly = branding.showName === false;
  return (
    <span
      className={`brand-identity${logoOnly ? ' brand-logo-only' : ''}`}
      role={logoOnly || sidebar ? 'img' : undefined}
      aria-label={logoOnly || sidebar ? branding.name : undefined}
    >
      {branding.logo ? (
        <img
          className={`platform-logo${sidebar && branding.collapsedLogo ? ' brand-expanded-logo' : ''}`}
          src={branding.logo}
          alt=""
        />
      ) : (
        <svg
          className={`logo-symbol domus-logo-mark${sidebar && branding.collapsedLogo ? ' brand-expanded-logo' : ''}`}
          viewBox="0 0 32 32"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.8"
          strokeLinecap="round"
          strokeLinejoin="round"
          aria-hidden="true"
        >
          <path d="M16 6v20M7 11h18M8 13l-4 7h8l-4-7ZM24 13l-4 7h8l-4-7ZM4 20c0 5 8 5 8 0M20 20c0 5 8 5 8 0M11 27h10M8 30h16" />
          <circle cx="16" cy="4" r="2" />
          <circle cx="5" cy="11" r="2" />
          <circle cx="27" cy="11" r="2" />
        </svg>
      )}
      {sidebar && branding.collapsedLogo && (
        <img className="platform-logo brand-collapsed-logo" src={branding.collapsedLogo} alt="" />
      )}
      {!logoOnly && <span className="logo-word">{branding.name}</span>}
      {!logoOnly && branding.name === 'domus' && <span className="logo-dot">®</span>}
    </span>
  );
}

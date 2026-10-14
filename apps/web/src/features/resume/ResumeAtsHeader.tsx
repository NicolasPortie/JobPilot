import { ChevronDown } from 'lucide-react'
import type { Profile } from '../../types'
import { initials } from './resumeAtsPresentation'

export function ResumeAtsHeader({ profile }: { profile: Pick<Profile, 'fullName' | 'targetRole' | 'seniority'> }) {
  return (
    <section className="cv-pagehead">
      <div className="cv-hero">
        <p className="cv-eyebrow">CURRÍCULO QUE TE APROXIMA</p>
        <h1 className="cv-h1">Adapte seu currículo para esta vaga</h1>
        <p className="cv-lead">
          Cole a vaga e receba, em segundos, uma versão verdadeira do seu currículo ajustada ao que ela pede.
          Você revisa, baixa o PDF e envia a candidatura.
        </p>
      </div>
      <div className="cv-top">
        <div className="cv-topbanner">
          <p className="cv-topbanner__text">Trabalho também<br />é futuro.</p>
          <div className="cv-topbanner__photo" role="img" aria-label="Ilustração de mesa de trabalho com planta e caneca">
            <svg viewBox="0 0 168 64" preserveAspectRatio="xMidYMid slice" aria-hidden focusable="false" style={{ display: 'block', width: '100%', height: '100%' }}>
              <defs>
                <linearGradient id="cv-desk" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0" stopColor="#f0c096" />
                  <stop offset="1" stopColor="#d69368" />
                </linearGradient>
                <linearGradient id="cv-pot" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0" stopColor="#c96f4a" />
                  <stop offset="1" stopColor="#a34e2c" />
                </linearGradient>
              </defs>
              <rect width="168" height="64" fill="url(#cv-desk)" />
              <ellipse cx="132" cy="10" rx="30" ry="14" fill="#f7d9b4" opacity="0.55" />
              <rect y="46" width="168" height="18" fill="#b57a4e" opacity="0.55" />
              <ellipse cx="118" cy="22" rx="7" ry="16" fill="#5d7f45" transform="rotate(-24 118 22)" />
              <ellipse cx="130" cy="18" rx="7" ry="18" fill="#6d9151" />
              <ellipse cx="142" cy="22" rx="7" ry="16" fill="#5d7f45" transform="rotate(24 142 22)" />
              <ellipse cx="124" cy="28" rx="6" ry="12" fill="#7fa35e" transform="rotate(-10 124 28)" />
              <ellipse cx="136" cy="28" rx="6" ry="12" fill="#7fa35e" transform="rotate(10 136 28)" />
              <path d="M116 38 h28 l-3 16 a3 3 0 0 1 -3 3 h-16 a3 3 0 0 1 -3 -3 Z" fill="url(#cv-pot)" />
              <rect x="112" y="35" width="36" height="5" rx="2.5" fill="#8c4526" />
              <rect x="34" y="30" width="26" height="24" rx="4" fill="#fdf3e3" />
              <rect x="34" y="30" width="26" height="7" rx="3.5" fill="#e8c9a4" />
              <ellipse cx="47" cy="33.5" rx="8" ry="2.6" fill="#7a4a26" />
              <path d="M60 36 h5 a6 6 0 0 1 0 12 h-6" fill="none" stroke="#fdf3e3" strokeWidth="4" />
              <ellipse cx="47" cy="56" rx="16" ry="3" fill="#8a5a34" opacity="0.35" />
              <ellipse cx="130" cy="58" rx="20" ry="3" fill="#8a5a34" opacity="0.35" />
            </svg>
          </div>
        </div>
        <div className="cv-userchip" aria-label="Conta ativa">
          <span className="cv-userchip__avatar" aria-hidden>{initials(profile.fullName)}</span>
          <span className="min-w-0">
            <span className="cv-userchip__name block truncate">{profile.fullName}</span>
            {(profile.targetRole || profile.seniority) && (
              <span className="cv-userchip__role block truncate">{profile.targetRole || profile.seniority}</span>
            )}
          </span>
          <ChevronDown size={15} className="shrink-0 text-[var(--cv-muted)]" />
        </div>
      </div>
    </section>
  )
}

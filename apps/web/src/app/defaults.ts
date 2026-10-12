import type { Profile, ResumeLayoutOption } from '../types'

export const emptyProfile: Profile = {
  fullName: '', email: '', phone: '', city: '', state: '', country: 'Brasil', linkedinUrl: '', githubUrl: '', portfolioUrl: '',
  professionalSummary: '', skills: [], targetRole: '', seniority: 'unknown', salaryExpectation: '',
}

export const fallbackResumeLayouts: ResumeLayoutOption[] = [
  { key: 'ats_standard', label: 'ATS padrão', description: 'Padrão recomendado: uma coluna ATS-safe, com contexto suficiente para experiência, projetos e palavras-chave reais.', atsNotes: 'Uma coluna, texto selecionável, sem tabelas, caixas, colunas, ícones, imagens, headers ou footers.', recommendedForAts: true },
  { key: 'ats_compact', label: 'ATS compacto', description: 'Use quando precisa tentar caber em uma pagina sem perder parsing ATS.', atsNotes: 'Mesmas regras ATS, com cortes mais agressivos de conteudo.', recommendedForAts: true },
  { key: 'human_review', label: 'Revisão humana', description: 'Versão para envio direto a uma pessoa, quando aparência e leitura humana importam mais que parser ATS.', atsNotes: 'Continua em uma coluna e texto selecionável, mas prioriza leitura confortável.', recommendedForAts: false },
]

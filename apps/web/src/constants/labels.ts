export const jobStatusLabels: Record<string, string> = {
  all: 'Todas', found: 'Encontrada', scored: 'Pontuadas', shortlisted: 'Favoritas', queued_to_apply: 'Na fila',
  ready_for_submit: 'Prontas', needs_review: 'Revisar', manual_only: 'Somente manual', applied: 'Candidatadas',
  ignored: 'Ignoradas', failed: 'Falhas', skipped: 'Pulada', opening: 'Abrindo', filling: 'Preenchendo',
}

export const runStateLabels: Record<string, string> = { running: 'Executando', completed: 'Concluido', failed: 'Falhou', aborted: 'Abortado' }

export const valueSourceLabels: Record<string, string> = {
  profile_field: 'Campo do perfil',
  'candidate-profile': 'Campo do perfil',
  reusable: 'Reutilizavel',
  'reusable-answer': 'Resposta salva',
  question_pattern: 'Regra salva',
  'question-pattern': 'Regra salva',
  ai_draft: 'Rascunho IA',
  'ai-draft': 'Rascunho IA',
  'fallback-policy': 'Politica automatica',
  checkpoint: 'Checkpoint',
  'manual-required': 'Manual',
}

export const actionTypeLabels: Record<string, string> = { click: 'Clique', fill: 'Preencher', select: 'Selecionar', check: 'Marcar', upload_file: 'Enviar arquivo', scroll: 'Rolar', wait: 'Aguardar', submit: 'Enviar', checkpoint: 'Checkpoint', done: 'Concluido', extract_job: 'Extrair vaga' }
export const pageStateLabels: Record<string, string> = { unknown: 'Desconhecido', login: 'Login', search: 'Busca', job_detail: 'Detalhe da vaga', application_form: 'Formulario', confirm: 'Confirmacao', done: 'Concluido', easy_apply_modal: 'Modal LinkedIn', external_application: 'Formulario externo', form: 'Formulario' }
export const riskLevelLabels: Record<string, string> = { none: 'Sem risco', low: 'Baixo', medium: 'Medio', high: 'Alto', critical: 'Critico', None: 'Sem risco', Low: 'Baixo', Medium: 'Medio', High: 'Alto', Critical: 'Critico' }
export const plannerLabels: Record<string, string> = { local: 'Local', local_required_guard: 'Guard local', local_fallback: 'Fallback local', llm: 'LLM', planner: 'Planner', completion_detector: 'Conclusao', block_detector: 'Bloqueio', stuck_detector: 'Stuck' }
export const guardVerdictLabels: Record<string, string> = { approved: 'Aprovado', target_not_observed: 'Alvo nao observado', target_type_incompatible: 'Tipo incompativel', navigation_blocked_by_required_fields: 'Obrigatorios pendentes', risk_requires_checkpoint: 'Checkpoint por risco', blocked_by_guard: 'Bloqueado' }
export const recoveryModeLabels: Record<string, string> = { None: 'Nenhuma', Retry: 'Retry', Reobserve: 'Reobservar', Scroll: 'Rolar', Remap: 'Remapear', Checkpoint: 'Checkpoint', Abort: 'Abortar' }
export const fieldTypeLabels: Record<string, string> = { text: 'Texto', email: 'E-mail', tel: 'Telefone', number: 'Numero', select: 'Selecao', checkbox: 'Checkbox', radio: 'Opcao', textarea: 'Texto longo', file: 'Arquivo', date: 'Data' }
export const stageLabels: Record<string, string> = { runtime: 'Execucao', login: 'Login', discover: 'Descoberta', apply: 'Candidatura', answer_review: 'Revisao de resposta' }
export const fieldNameLabels: Record<string, string> = { Phone: 'Telefone', Email: 'E-mail', ResumePath: 'Curriculo', SalaryExpectation: 'Pretensao salarial', Skills: 'Habilidades', FullName: 'Nome completo', City: 'Cidade', ProfessionalSummary: 'Resumo profissional' }

export const checkpointReasonLabels: Record<string, string> = {
  checkpoint_required: 'Checkpoint necessario', validation_checkpoint: 'Validacao necessaria', stuck_detection: 'Travamento detectado',
  unrecognized_flow: 'Fluxo nao reconhecido', action_failed: 'Acao falhou', captcha_or_security: 'Captcha / Seguranca',
  linkedin_security_verification: 'Verificacao LinkedIn', login_required: 'Login necessario', login_or_security_checkpoint: 'Login ou seguranca',
  login_or_security: 'Login ou verificacao',
  resume_upload_requires_manual_selection: 'Anexar curriculo',
  answer_needs_confirmation: 'Resposta provisoria', external_apply_not_opened: 'Formulario externo nao abriu',
  application_surface_not_found: 'Botao de candidatura nao encontrado', easy_apply_modal_not_opened: 'Modal LinkedIn nao abriu',
}

export const eventTypeLabels: Record<string, string> = {
  'automation.discover.started': 'Busca iniciada', 'automation.apply.started': 'Piloto iniciado', 'automation.apply.no_queue': 'Sem vagas na fila',
  'discover.returning_to_search': 'Retornando a busca', 'discover.filtered': 'Vaga filtrada', 'discover.filter_applied': 'Filtros aplicados',
  'discover.completed': 'Descoberta concluida', 'run.aborted': 'Execucao abortada', 'job.failed': 'Vaga falhou',
  'run.limit_reached': 'Limite atingido', 'job.opening': 'Abrindo vaga', 'job.returning_to_lead': 'Retornando a vaga',
  'job.auto_submit': 'Envio automatico', 'job.applied': 'Candidatado', 'runtime.observe': 'Observacao',
  'runtime.action': 'Acao executada', 'linkedin.login.started': 'Login iniciado', 'linkedin.login.fields_missing': 'Campos nao encontrados',
  'linkedin.login.waiting_security': 'Aguardando verificacao', 'linkedin.login.failed': 'Login falhou', 'linkedin.login.completed': 'Login OK',
  'linkedin.external_apply_opened': 'LinkedIn -> formulario externo', 'application_surface.opened': 'Superficie de candidatura aberta',
  'application_surface.missing': 'Superficie de candidatura nao encontrada',
}

export const jobStatuses = Object.keys(jobStatusLabels)

export function statusBadge(status: string) {
  const map: Record<string, string> = {
    shortlisted: 'badge-amber', queued_to_apply: 'badge-blue', ready_for_submit: 'badge-purple',
    applied: 'badge-green', ignored: 'badge-slate', failed: 'badge-red', needs_review: 'badge-red',
    manual_only: 'badge-slate', skipped: 'badge-slate', scored: 'badge-teal', found: 'badge-slate',
  }
  return map[status] ?? 'badge-slate'
}

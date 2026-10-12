# JobPilot Design System

> Sistema de design para o JobPilot — plataforma de busca de emprego e ATS.
> Stack: React 19 + Vite + TypeScript + CSS Modules

---

## Filosofia de Design

JobPilot é uma ferramenta que ajuda pessoas a encontrar empregos e gerenciar candidaturas. O design deve transmitir:

- **Profissionalismo** — confiável, sério
- **Otimismo** — encontrar um emprego é uma jornada positiva
- **Eficiência** — informação clara, ações rápidas
- **Humanidade** — não é um software corporativo frio

---

## Cores

### Primária — Azul Confiança

| Token | Hex | Uso |
|-------|-----|-----|
| `--color-primary-50` | `#eff6ff` | Fundos sutis |
| `--color-primary-100` | `#dbeafe` | Focus rings |
| `--color-primary-500` | `#3b82f6` | Ícones, links |
| `--color-primary-600` | `#2563eb` | **Botões primários, CTAs** |
| `--color-primary-700` | `#1d4ed8` | Hover states |
| `--color-primary-900` | `#1e3a8a` | Texto em fundos claros |

### Semânticas

| Token | Hex | Uso |
|-------|-----|-----|
| `--color-success-500` | `#22c55e` | Sucesso, aprovado |
| `--color-success-600` | `#16a34a` | Hover success |
| `--color-warning-500` | `#f59e0b` | Alerta, pendente |
| `--color-warning-600` | `#d97706` | Hover warning |
| `--color-error-500` | `#ef4444` | Erro, rejeitado |
| `--color-error-600` | `#dc2626` | Hover error |

### Neutros — Slate (com leve tom azul)

| Token | Hex | Uso |
|-------|-----|-----|
| `--color-white` | `#ffffff` | Cards, fundos |
| `--color-gray-50` | `#f8fafc` | Fundo de página |
| `--color-gray-100` | `#f1f5f9` | Hover, divisórias |
| `--color-gray-200` | `#e2e8f0` | **Bordas padrão** |
| `--color-gray-300` | `#cbd5e1` | Bordas hover |
| `--color-gray-400` | `#94a3b8` | Placeholder |
| `--color-gray-500` | `#64748b` | Texto secundário |
| `--color-gray-600` | `#475569` | Texto terciário |
| `--color-gray-700` | `#334155` | Texto secundário escuro |
| `--color-gray-800` | `#1e293b` | Subtítulos |
| `--color-gray-900` | `#0f172a` | **Texto principal** |

> ⚠️ **NUNCA use `#000000` preto puro.** O `--color-gray-900` é o mais escuro permitido.

---

## Tipografia

### Famílias

```css
--font-sans: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
--font-mono: 'JetBrains Mono', 'Fira Code', monospace;
```

### Escala

| Token | Tamanho | Uso |
|-------|---------|-----|
| `--text-xs` | 12px | Badges, legendas |
| `--text-sm` | 14px | Texto secundário |
| `--text-base` | 16px | **Corpo de texto** |
| `--text-lg` | 18px | Destaques |
| `--text-xl` | 20px | Títulos pequenos |
| `--text-2xl` | 24px | H3 |
| `--text-3xl` | 30px | H2 |
| `--text-4xl` | 36px | H1 |
| `--text-5xl` | 48px | Hero |

### Hierarquia

```
H1: 36px, weight 700, gray-900, line-height 1.25
H2: 30px, weight 700, gray-900, line-height 1.25
H3: 24px, weight 600, gray-800, line-height 1.375
Body: 16px, weight 400, gray-600, line-height 1.5
Caption: 14px, weight 400, gray-500
```

---

## Espaçamento

Base: **4px**

| Token | Valor | Uso típico |
|-------|-------|------------|
| `--space-1` | 4px | Ícones pequenos |
| `--space-2` | 8px | Gap entre elementos próximos |
| `--space-3` | 12px | Padding interno pequeno |
| `--space-4` | 16px | **Padrão** |
| `--space-6` | 24px | Padding de card |
| `--space-8` | 32px | Seções |
| `--space-12` | 48px | Grandes seções |
| `--space-16` | 64px | Hero sections |

---

## Elevação (Sombras)

| Token | Valor | Uso |
|-------|-------|-----|
| `--shadow-sm` | 0 1px 2px rgba(0,0,0,0.05) | Botões, inputs |
| `--shadow-md` | 0 4px 6px rgba(0,0,0,0.1) | **Cards padrão** |
| `--shadow-lg` | 0 10px 15px rgba(0,0,0,0.1) | Cards hover, dropdowns |
| `--shadow-xl` | 0 20px 25px rgba(0,0,0,0.1) | Modais |
| `--shadow-2xl` | 0 25px 50px rgba(0,0,0,0.25) | Overlays |

---

## Bordas Arredondadas

| Token | Valor | Uso |
|-------|-------|-----|
| `--radius-sm` | 4px | Tags |
| `--radius-md` | 6px | — |
| `--radius-lg` | 8px | **Botões, inputs** |
| `--radius-xl` | 12px | **Cards** |
| `--radius-2xl` | 16px | Modais |
| `--radius-full` | 9999px | Badges, avatares |

---

## Componentes

### Botão Primário

```
Background: primary-600
Hover: primary-700
Active: scale(0.98)
Text: white, semibold
Padding: 12px 24px
Radius: 8px
Shadow: shadow-sm → shadow-md no hover
Focus: ring 3px primary-100
Transition: all 200ms ease-out
```

### Botão Secundário

```
Background: white
Border: 1px solid gray-300
Hover: gray-50, border gray-400
Text: gray-700, semibold
Padding: 12px 24px
Radius: 8px
Focus: ring 3px gray-100
```

### Botão Ghost

```
Background: transparent
Hover: gray-100
Text: gray-600, semibold
Padding: 12px 24px
Radius: 8px
```

### Card

```
Background: white
Border: 1px solid gray-200
Radius: 12px
Shadow: shadow-sm
Padding: 24px
Hover: shadow-lg, translateY(-2px)
Transition: 300ms ease-out
```

### Input

```
Background: white
Border: 1px solid gray-300
Radius: 8px
Padding: 12px 16px
Focus: border primary-500, ring primary-100
Placeholder: gray-400
Error: border error-500, ring error-100
```

### Badge

```
Padding: 4px 12px
Font: 12px, semibold, uppercase, tracking-wide
Radius: 9999px (pill)
Variants:
  - Primary: bg primary-50, text primary-700
  - Success: bg success-50, text success-600
  - Warning: bg warning-50, text warning-600
  - Error: bg error-50, text error-600
  - Neutral: bg gray-100, text gray-700
```

---

## Padrões Específicos do JobPilot

### Card de Vaga

```
┌────────────────────────────────────────┐
│ [Logo]  Título da Vaga              [♥]│
│         Empresa • Cidade, Estado       │
│                                        │
│ [Remoto] [Tempo integral] [Pleno]      │
│                                        │
│ R$ 8.000 - R$ 12.000        2 dias atrás│
│                             [Candidatar]│
└────────────────────────────────────────┘
```

Regras:
- Logo: 48x48px, border-radius 8px
- Título: primary-600 no hover (se clicável)
- Tags: badges no variant "neutral"
- Salário: gray-700, font-weight 500
- Data: gray-500, text-sm
- Ações: ícones no canto superior direito
- CTA "Candidatar": botão primário, full-width no mobile

### Card de Estatística (Dashboard)

```
┌──────────────────┐
│ [🔵]             │
│ 1.247            │
│ Candidaturas     │
│ ↑ 12% vs mês     │
└──────────────────┘
```

Regras:
- Ícone em círculo com fundo colorido (primary-100, ícone primary-600)
- Número: text-3xl, font-bold, gray-900
- Label: text-sm, gray-500
- Variação: com ícone de seta, green-600 para positivo, red-600 para negativo

### Timeline de Candidatura

```
●───●───●───○───○
↑   ↑   ↑       
Aplicado  Entrevista  Oferta  Contratado
```

Regras:
- Passos completos: círculo preenchido (primary-600)
- Passo atual: círculo preenchido com anel (primary-600 + ring)
- Passos futuros: círculo vazio (gray-300)
- Linha entre passos: primary-600 para completos, gray-200 para futuros
- Label abaixo: text-sm, gray-600 (primary-700 para atual)

### Estado Vazio

```
┌──────────────────────────┐
│                          │
│     [Ilustração/Ícone]   │
│                          │
│   Nenhuma candidatura    │
│   Você ainda não se      │
│   candidatou a nenhuma   │
│   vaga.                  │
│                          │
│   [Explorar Vagas]       │
│                          │
└──────────────────────────┘
```

Regras:
- Ícone: 64x64px, gray-400
- Título: text-xl, font-semibold, gray-900
- Descrição: text-base, gray-500, max-width 320px, centralizado
- CTA: botão primário
- NUNCA usar "No data" ou "Empty"

---

## Animações

### Micro-interações

```css
/* Hover em cards */
transition: box-shadow 300ms ease-out, transform 300ms ease-out;
/* hover: */
box-shadow: var(--shadow-lg);
transform: translateY(-2px);

/* Hover em botões */
transition: all 200ms ease-out;
/* hover: */
background: var(--color-primary-700);
box-shadow: var(--shadow-md);

/* Focus */
transition: box-shadow 200ms ease-out;
/* focus: */
box-shadow: 0 0 0 3px var(--color-primary-100);

/* Active/Click */
transform: scale(0.98);
```

### Loading

```css
/* Skeleton pulse */
@keyframes skeleton-pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}
.skeleton {
  background: var(--color-gray-200);
  border-radius: var(--radius-lg);
  animation: skeleton-pulse 2s ease-in-out infinite;
}
```

### Página

```css
/* Staggered fade-in */
@keyframes fade-in-up {
  from {
    opacity: 0;
    transform: translateY(10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.item {
  animation: fade-in-up 400ms ease-out forwards;
}
.item:nth-child(1) { animation-delay: 0ms; }
.item:nth-child(2) { animation-delay: 50ms; }
.item:nth-child(3) { animation-delay: 100ms; }
/* etc */
```

> ⚠️ **Respeitar `prefers-reduced-motion: reduce`**

---

## Responsividade

### Breakpoints

| Nome | Largura | Dispositivo |
|------|---------|-------------|
| `sm` | 640px+ | Celular landscape |
| `md` | 768px+ | Tablet |
| `lg` | 1024px+ | Laptop |
| `xl` | 1280px+ | Desktop |

### Padrões

```
Grid de cards:
  Mobile: 1 coluna
  Tablet: 2 colunas
  Desktop: 3 colunas

Navegação:
  Mobile: hamburger menu, bottom sheet
  Desktop: barra horizontal fixa

Cards de vaga:
  Mobile: CTA embaixo, full-width
  Desktop: CTA na direita, auto-width
```

---

## Acessibilidade

- **WCAG AA obrigatório** para todo novo componente
- Contraste mínimo: 4.5:1 para texto normal, 3:1 para texto grande
- Focus visível: 3px outline ou ring
- Todos elementos interativos acessíveis por teclado
- Botões com ícone apenas: `aria-label` obrigatório
- Inputs: `<label>` associado sempre
- Imagens: `alt` sempre
- Targets de toque: mínimo 44x44px no mobile

---

## Anti-Padrões

| ❌ Nunca Fazer | ✅ Fazer Em Vez |
|----------------|----------------|
| `#000000` puro | `--color-gray-900` |
| Gradientes roxo-azul genéricos | Cores sólidas do sistema |
| `!important` | Especificidade CSS correta |
| Texto < 12px | `--text-xs` (12px) no mínimo |
| Sombras fortes sem blur | Sombras sutis com blur |
| Bordas coloridas grossas | Background tints ou badges |
| Cores sozinhas para status | Ícone + cor + texto |
| Cards dentro de cards | Separação por espaço |
| Animações de layout (width, height) | transform e opacity apenas |
| Fontes genéricas (Arial, Roboto) | Inter com fallbacks |

---

## Recursos

- **Ícones**: [Lucide React](https://lucide.dev)
- **Fonte**: [Inter](https://fonts.google.com/specimen/Inter) (Google Fonts)
- **Cores**: Baseado no Tailwind Slate + Blue
- **Inspiração**: Linear (clean), Stripe (professional), Notion (warm)

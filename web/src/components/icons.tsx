import type { SVGProps } from 'react'

/**
 * Small line icons drawn inline (24×24, stroke = currentColor), so they
 * follow the text colour in light and dark mode and need no icon package.
 * Decorative by default: the text or aria-label next to them carries the meaning.
 */
function Icon({ children, ...props }: SVGProps<SVGSVGElement>) {
  return (
    <svg
      viewBox="0 0 24 24"
      width="20"
      height="20"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
      {...props}
    >
      {children}
    </svg>
  )
}

type P = SVGProps<SVGSVGElement>

export const PlusIcon = (p: P) => (
  <Icon {...p}>
    <path d="M12 5v14M5 12h14" />
  </Icon>
)

export const SearchIcon = (p: P) => (
  <Icon {...p}>
    <circle cx="11" cy="11" r="7" />
    <path d="M21 21l-4.3-4.3" />
  </Icon>
)

export const CloseIcon = (p: P) => (
  <Icon {...p}>
    <path d="M18 6L6 18M6 6l12 12" />
  </Icon>
)

export const MailIcon = (p: P) => (
  <Icon {...p}>
    <rect x="2" y="4" width="20" height="16" rx="2" />
    <path d="M22 7l-10 6L2 7" />
  </Icon>
)

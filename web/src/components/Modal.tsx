import { useEffect, useId, useRef, type ReactNode } from 'react'
import { CloseIcon } from './icons'

interface Props {
  title: string
  onClose: () => void
  children: ReactNode
}

/**
 * A pop-up window over the page. Keyboard-friendly: focus moves into it
 * when it opens, Escape or a click on the dimmed background closes it, and
 * focus goes back to whatever opened it.
 */
export function Modal({ title, onClose, children }: Props) {
  const titleId = useId()
  const panel = useRef<HTMLDivElement>(null)
  // The latest onClose, without re-running the effect (and re-focusing) on every render.
  const close = useRef(onClose)
  useEffect(() => {
    close.current = onClose
  })

  useEffect(() => {
    const opener = document.activeElement as HTMLElement | null
    // Focus the first field, or the panel itself if there is none.
    const first = panel.current?.querySelector<HTMLElement>('input, select, textarea')
    ;(first ?? panel.current)?.focus()

    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && close.current()
    document.addEventListener('keydown', onKey)
    // Stop the page underneath from scrolling while the window is open.
    const overflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      document.removeEventListener('keydown', onKey)
      document.body.style.overflow = overflow
      opener?.focus?.()
    }
  }, [])

  return (
    <div className="modal-backdrop" onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
      <div className="modal" role="dialog" aria-modal="true" aria-labelledby={titleId} ref={panel} tabIndex={-1}>
        <div className="modal-head">
          <h2 id={titleId}>{title}</h2>
          <button type="button" className="icon-button" onClick={onClose} aria-label="Close">
            <CloseIcon />
          </button>
        </div>
        {children}
      </div>
    </div>
  )
}

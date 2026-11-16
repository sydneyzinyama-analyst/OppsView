import { createContext, useContext } from 'react'

/**
 * Lets any page ask the bell to recount ("a reminder was just dealt with").
 * A React context, so pages don't need to know where the bell lives.
 */
export const BellContext = createContext<() => void>(() => {})

export const useRefreshBell = () => useContext(BellContext)

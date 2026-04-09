import { useAuth0 } from '@auth0/auth0-react'

export function LoginButton() {
  const { loginWithRedirect } = useAuth0()

  return (
    <button
      onClick={() => loginWithRedirect()}
      className="rounded bg-cortex-blue px-4 py-2 text-white transition-colors hover:bg-cortex-blue-dark"
    >
      Log In
    </button>
  )
}

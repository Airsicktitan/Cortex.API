import { useAuth0 } from '@auth0/auth0-react'

export function LogoutButton() {
  const { logout } = useAuth0()

  return (
    <button
      onClick={() => logout({ logoutParams: { returnTo: window.location.origin } })}
      className="px-4 py-2 bg-red-600 text-white rounded hover:bg-red-700"
    >
      Log Out
    </button>
  )
}
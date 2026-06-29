import { useState } from 'react'
import { useUsers, useUpdateUserRole, useDeactivateUser, useActivateUser, useUpdateUserProfile } from '../hooks/useUsers'
import StatusChip from '../components/ui/StatusChip'
import Pagination from '../components/Pagination'
import { api } from '../api/client'

const ALL_ROLES = [
  'Administrator', 'Operator', 'Supervisor', 'Auditor',
  'SecurityOfficer', 'Student',
]

const PAGE_SIZE = 20

const FILTER_ROLES = ['', ...ALL_ROLES]

export default function UsersPage() {
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [roleFilter, setRoleFilter] = useState('')
  const [activeFilter, setActiveFilter] = useState<'' | 'true' | 'false'>('')
  const isActiveParam = activeFilter === '' ? undefined : activeFilter === 'true'
  const { data, isLoading } = useUsers(
    page, PAGE_SIZE,
    search || undefined, roleFilter || undefined,
    isActiveParam
  )
  const updateRole    = useUpdateUserRole()
  const deactivate    = useDeactivateUser()
  const activate      = useActivateUser()
  const updateProfile = useUpdateUserProfile()
  const [editingDisplayName, setEditingDisplayName] = useState<Record<string, string>>({})

  if (isLoading) return <p>Loading...</p>

  const users = data?.users ?? []

  return (
    <div className="space-y-5 pb-4">
      {/* Header */}
      <div className="glass-card px-6 py-4">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-xl font-bold text-foreground">Users</h1>
            {data && (
              <p className="text-sm text-muted-foreground mt-0.5">{data.totalCount} total users</p>
            )}
          </div>
        </div>
      </div>

      {/* Filters */}
      <div className="glass-card p-4">
        <div className="flex flex-wrap gap-3">
          <input
            value={search}
            onChange={e => { setSearch(e.target.value); setPage(1) }}
            placeholder="Search by email…"
            className="input-glass flex-1 min-w-48"
          />
          <select
            value={roleFilter}
            onChange={e => { setRoleFilter(e.target.value); setPage(1) }}
            className="input-glass w-40"
          >
            {FILTER_ROLES.map(r => (
              <option key={r} value={r}>{r || 'All roles'}</option>
            ))}
          </select>
          <select
            value={activeFilter}
            onChange={e => { setActiveFilter(e.target.value as '' | 'true' | 'false'); setPage(1) }}
            className="input-glass w-36"
          >
            <option value="">All statuses</option>
            <option value="true">Active</option>
            <option value="false">Inactive</option>
          </select>
          <button
            onClick={() => api.exportUsers(search || undefined, roleFilter || undefined, isActiveParam).then(blob => {
              const url = URL.createObjectURL(blob)
              const a = document.createElement('a')
              a.href = url
              a.download = `users-${Date.now()}.csv`
              a.click()
              URL.revokeObjectURL(url)
            })}
            className="btn-glass text-xs px-4 py-2"
          >
            Export CSV
          </button>
          {(search || roleFilter || activeFilter) && (
            <button
              onClick={() => { setSearch(''); setRoleFilter(''); setActiveFilter(''); setPage(1) }}
              className="text-sm text-muted-foreground hover:text-foreground px-2 transition-colors"
            >
              Clear
            </button>
          )}
        </div>
      </div>

      {users.length === 0 ? (
        <div className="glass-card p-12 text-center text-muted-foreground">No users found.</div>
      ) : (
        <div className="glass-card overflow-hidden">
          <table className="glass-table w-full">
            <thead>
              <tr>
                <th>Email</th>
                <th>Display Name</th>
                <th>Role</th>
                <th>Status</th>
                <th>Created</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {users.map(user => (
                <tr key={user.userId}>
                  <td className="text-sm text-foreground">{user.email}</td>
                  <td>
                    <input
                      type="text"
                      placeholder="—"
                      value={editingDisplayName[user.userId] ?? ''}
                      onChange={e => setEditingDisplayName(p => ({ ...p, [user.userId]: e.target.value }))}
                      onBlur={e => {
                        const val = e.target.value.trim() || null
                        updateProfile.mutate({ userId: user.userId, displayName: val })
                      }}
                      className="input-glass w-36 text-xs py-1.5 px-2.5"
                    />
                  </td>
                  <td>
                    <select
                      value={user.role}
                      onChange={e => updateRole.mutate({ userId: user.userId, role: e.target.value })}
                      className="input-glass w-auto text-xs py-1.5 px-2.5"
                      disabled={updateRole.isPending}
                    >
                      {ALL_ROLES.map(r => (
                        <option key={r} value={r}>{r}</option>
                      ))}
                    </select>
                  </td>
                  <td>
                    <StatusChip
                      variant={user.isActive ? 'success' : 'muted'}
                      label={user.isActive ? 'Active' : 'Inactive'}
                    />
                  </td>
                  <td className="text-sm text-muted-foreground">
                    {new Date(user.createdAt).toLocaleDateString()}
                  </td>
                  <td>
                    {user.isActive ? (
                      <button
                        onClick={() => deactivate.mutate(user.userId)}
                        disabled={deactivate.isPending}
                        className="btn-danger"
                      >
                        Deactivate
                      </button>
                    ) : (
                      <button
                        onClick={() => activate.mutate(user.userId)}
                        disabled={activate.isPending}
                        className="btn-success"
                      >
                        Activate
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <Pagination
            page={page}
            totalPages={data?.totalPages ?? 1}
            onPageChange={setPage}
          />
        </div>
      )}
    </div>
  )
}

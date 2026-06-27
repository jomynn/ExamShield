import { useState } from 'react'
import { useUsers, useUpdateUserRole, useDeactivateUser } from '../hooks/useUsers'
import StatusChip from '../components/ui/StatusChip'
import Pagination from '../components/Pagination'

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
  const { data, isLoading } = useUsers(page, PAGE_SIZE, search || undefined, roleFilter || undefined)
  const updateRole = useUpdateUserRole()
  const deactivate = useDeactivateUser()

  if (isLoading) return <p>Loading...</p>

  const users = data?.users ?? []

  return (
    <div className="p-6 space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Users</h1>
        {data && (
          <span className="text-sm text-muted-foreground">{data.totalCount} total</span>
        )}
      </div>

      <div className="flex gap-2">
        <input
          value={search}
          onChange={e => { setSearch(e.target.value); setPage(1) }}
          placeholder="Search by email…"
          className="flex-1 rounded border border-[#30363D] bg-[#161B22] px-3 py-1.5 text-sm text-white placeholder-[#8B949E]"
        />
        <select
          value={roleFilter}
          onChange={e => { setRoleFilter(e.target.value); setPage(1) }}
          className="rounded border border-[#30363D] bg-[#161B22] px-3 py-1.5 text-sm text-white"
        >
          {FILTER_ROLES.map(r => (
            <option key={r} value={r}>{r || 'All roles'}</option>
          ))}
        </select>
        {(search || roleFilter) && (
          <button
            onClick={() => { setSearch(''); setRoleFilter(''); setPage(1) }}
            className="text-sm text-[#8B949E] hover:text-white px-2"
          >
            Clear
          </button>
        )}
      </div>

      {users.length === 0 ? (
        <p className="text-muted-foreground">No users found.</p>
      ) : (
        <div className="rounded-lg border border-border overflow-hidden">
          <table className="w-full text-sm border-collapse">
            <thead>
              <tr className="border-b text-left bg-muted/20">
                <th className="py-2 px-4">Email</th>
                <th className="py-2 px-4">Role</th>
                <th className="py-2 px-4">Status</th>
                <th className="py-2 px-4">Created</th>
                <th className="py-2 px-4" />
              </tr>
            </thead>
            <tbody>
              {users.map(user => (
                <tr key={user.userId} className="border-b hover:bg-muted/30">
                  <td className="py-2 px-4">{user.email}</td>
                  <td className="py-2 px-4">
                    <select
                      value={user.role}
                      onChange={e =>
                        updateRole.mutate({ userId: user.userId, role: e.target.value })
                      }
                      className="rounded border px-2 py-1 text-xs bg-background"
                      disabled={updateRole.isPending}
                    >
                      {ALL_ROLES.map(r => (
                        <option key={r} value={r}>{r}</option>
                      ))}
                    </select>
                  </td>
                  <td className="py-2 px-4">
                    <StatusChip
                      variant={user.isActive ? 'success' : 'muted'}
                      label={user.isActive ? 'Active' : 'Inactive'}
                    />
                  </td>
                  <td className="py-2 px-4 text-muted-foreground">
                    {new Date(user.createdAt).toLocaleDateString()}
                  </td>
                  <td className="py-2 px-4">
                    {user.isActive && (
                      <button
                        onClick={() => deactivate.mutate(user.userId)}
                        disabled={deactivate.isPending}
                        className="px-3 py-1 text-xs rounded border border-red-500 text-red-500 hover:bg-red-500/10 disabled:opacity-50"
                      >
                        Deactivate
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

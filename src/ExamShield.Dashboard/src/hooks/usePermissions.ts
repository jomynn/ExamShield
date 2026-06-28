const IMAGE_VIEW_ROLES = new Set([
  'Operator',
  'Invigilator',
  'Supervisor',
  'ManualReviewer',
  'ReviewSupervisor',
  'InvestigationOfficer',
])

export function usePermissions(role: string | null) {
  return {
    canViewImage: IMAGE_VIEW_ROLES.has(role ?? ''),
  }
}

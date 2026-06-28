import { describe, it, expect } from 'vitest'
import { usePermissions } from '../hooks/usePermissions'

const IMAGE_ALLOWED = [
  'Operator',
  'Invigilator',
  'Supervisor',
  'ManualReviewer',
  'ReviewSupervisor',
  'InvestigationOfficer',
]

const IMAGE_BLOCKED = [
  'Administrator',
  'SuperAdministrator',
  'SecurityOfficer',
  'SecurityAdministrator',
  'SystemAdministrator',
  'Auditor',
  'ExamManager',
  'DeviceManager',
  'ResultPublisher',
  'ScoringEngine',
  'Student',
  'PublicVerification',
]

describe('usePermissions', () => {
  describe('canViewImage', () => {
    it.each(IMAGE_ALLOWED)('%s can view image', (role) => {
      expect(usePermissions(role).canViewImage).toBe(true)
    })

    it.each(IMAGE_BLOCKED)('%s cannot view image', (role) => {
      expect(usePermissions(role).canViewImage).toBe(false)
    })

    it('null role cannot view image', () => {
      expect(usePermissions(null).canViewImage).toBe(false)
    })

    it('unknown role cannot view image', () => {
      expect(usePermissions('UnknownRole').canViewImage).toBe(false)
    })
  })
})

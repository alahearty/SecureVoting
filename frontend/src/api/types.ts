export type Role =
  | 'SuperAdministrator'
  | 'ElectoralOfficer'
  | 'Student'
  | 'Candidate'
  | 'Observer'

export type ElectionStatus =
  | 'Draft'
  | 'Scheduled'
  | 'Open'
  | 'Closed'
  | 'UnderReview'
  | 'Published'
  | 'Suspended'
  | 'Cancelled'

export type CandidateStatus = 'Pending' | 'Approved' | 'Rejected' | 'Withdrawn' | 'Disqualified'
export type ResultStatus = 'Provisional' | 'Verified' | 'Published' | 'Superseded'
export type TieHandling = 'RunoffRequired' | 'SharedOffice' | 'ManualResolution'
export type AnnouncementAudience = 'Everyone' | 'EligibleVoters' | 'Candidates' | 'Observers'

export interface Meta {
  applicationName: string
  institution: string
  demoMode: boolean
  serverTimeUtc: string
  version: string
}

export interface Me {
  userId: string
  displayName: string
  email: string | null
  matricNumber: string | null
  roles: Role[]
  hasStudentProfile: boolean
  hasCandidateProfile: boolean
  department: string | null
  isDemo: boolean
}

export interface ElectionSummary {
  id: string
  title: string
  description: string
  academicSession: string
  departmentId: string
  departmentCode: string
  department: string
  status: ElectionStatus
  nominationOpensAt: string
  nominationClosesAt: string
  opensAt: string
  closesAt: string
  eligibleVoters: number
  acceptedBallots: number
  positionCount: number
  approvedCandidates: number
  isEligible: boolean
  hasVoted: boolean
  votedAt: string | null
  currentResultId: string | null
  publishedAt: string | null
}

export interface CandidatePublic {
  id: string
  displayName: string
  manifesto: string
  photoUrl: string | null
}

export interface PositionPublic {
  id: string
  title: string
  description: string
  seatCount: number
  maxSelections: number
  candidates: CandidatePublic[]
}

export interface ElectionRule {
  tieHandling: TieHandling
  allowAbstention: boolean
  nominationRequiresApproval: boolean
}

export interface ElectionDetail {
  id: string
  title: string
  description: string
  academicSession: string
  departmentId: string
  departmentCode: string
  department: string
  status: ElectionStatus
  nominationOpensAt: string
  nominationClosesAt: string
  opensAt: string
  closesAt: string
  rule: ElectionRule
  positions: PositionPublic[]
  suspensionReason: string | null
  cancellationReason: string | null
  publishedAt: string | null
}

export interface Ballot {
  electionId: string
  title: string
  departmentId: string
  departmentCode: string
  department: string
  closesAt: string
  hasVoted: boolean
  votedAt: string | null
  isEligible: boolean
  positions: BallotPosition[]
}

export interface BallotPosition {
  id: string
  title: string
  description: string
  seatCount: number
  maxSelections: number
  allowAbstention: boolean
  candidates: BallotCandidate[]
}

export interface BallotCandidate {
  id: string
  displayName: string
  manifesto: string
  photoUrl: string | null
}

export interface PositionChoice {
  positionId: string
  candidateIds: string[]
}

export interface BallotAccepted {
  electionId: string
  submittedAtUtc: string
  confirmationReference: string
  message: string
}

export interface VoteStatus {
  electionId: string
  hasVoted: boolean
  votedAt: string | null
  confirmationHint: string | null
}

export interface CandidateProfile {
  id: string
  electionId: string
  electionTitle: string
  departmentId: string
  departmentCode: string
  departmentName: string
  positionId: string
  positionTitle: string
  displayName: string
  manifesto: string
  photoUrl: string | null
  status: CandidateStatus
  rejectionReason: string | null
  withdrawalReason: string | null
  submittedAt: string
  reviewedAt: string | null
}

export interface CandidateAdmin extends CandidateProfile {
  studentId: string
  matricNumber: string
  studentName: string
}

export interface ElectionResult {
  id: string
  electionId: string
  electionTitle: string
  version: number
  status: ResultStatus
  totalEligible: number
  totalBallots: number
  turnoutPercentage: number
  generatedAt: string
  verifiedAt: string | null
  publishedAt: string | null
  integrityHash: string
  positions: ResultPosition[]
  approvals: ResultApproval[]
  changeReason: string | null
}

export interface ResultPosition {
  positionId: string
  positionTitle: string
  seatCount: number
  abstentions: number
  candidates: ResultCandidate[]
  tieNote: string | null
}

export interface ResultCandidate {
  candidateId: string
  candidateName: string
  votes: number
  percentage: number
  rank: number
  isWinner: boolean
  isTie: boolean
}

export interface ResultApproval {
  role: string
  approvalType: string
  decision: string
  notes: string | null
  createdAt: string
}

export interface AuditLog {
  sequence: number
  eventId: string
  occurredAt: string
  actorUserId: string | null
  actorDisplay: string
  eventType: string
  entityType: string
  entityId: string | null
  outcome: string
  correlationId: string | null
  detailsJson: string
  hash: string
}

export interface AuditVerification {
  isValid: boolean
  entriesChecked: number
  brokenAtSequence: number | null
  message: string
  lastHash: string
}

export interface Announcement {
  id: string
  electionId: string | null
  electionTitle: string | null
  title: string
  body: string
  audience: AnnouncementAudience
  publishedAt: string
}

export interface Notification {
  id: string
  type: string
  subject: string
  body: string
  createdAt: string
  isRead: boolean
}

export interface Metric {
  label: string
  value: string
  detail: string | null
}

export interface ChartPoint {
  label: string
  value: number
}

export interface ChartSeries {
  label: string
  points: ChartPoint[]
}

export interface AdminDashboard {
  metrics: Metric[]
  activeElections: ElectionSummary[]
  recentAudit: AuditLog[]
  turnoutByElection: ChartSeries
}

export interface StudentDashboard {
  elections: ElectionSummary[]
  announcements: Announcement[]
  notifications: Notification[]
}

export interface ObserverDashboard {
  metrics: Metric[]
  elections: ElectionSummary[]
  recentAudit: AuditLog[]
  turnoutByElection: ChartSeries
}

export interface CandidateDashboard {
  nomination: CandidateProfile | null
  announcements: Announcement[]
}

export interface EligibleVoter {
  studentId: string
  matricNumber: string
  fullName: string
  email: string
  level: number
  isActive: boolean
  hasAccount: boolean
  hasVoted: boolean
  votedAt: string | null
}

export interface ActivationCredential {
  matricNumber: string
  token: string
  expiresAt: string
}

export interface VoterImportResponse {
  rowsProcessed: number
  eligibleCreated: number
  eligibilityUpdated: number
  accountsCreated: number
  duplicateRows: number
  errors: Array<{ rowNumber: number; message: string }>
  activationTokens: ActivationCredential[]
}

export type DepartmentAccessScope = 'ElectoralOfficer' | 'Observer'

export interface Department {
  id: string
  name: string
  code: string
  faculty: string | null
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface DepartmentAssignment {
  departmentId: string
  departmentName: string
  departmentCode: string
  scope: DepartmentAccessScope
}

export interface DepartmentAssignmentInput {
  departmentId: string
  scope: DepartmentAccessScope
}

export interface UserSummary {
  id: string
  displayName: string
  email: string | null
  matricNumber: string | null
  isActive: boolean
  isLockedOut: boolean
  roles: Role[]
  createdAt: string
}

export interface SystemSetting {
  key: string
  value: string
  updatedAt: string
}

export interface CreateElectionInput {
  title: string
  description: string
  academicSession: string
  departmentId: string
  nominationOpensAt: string
  nominationClosesAt: string
  opensAt: string
  closesAt: string
  allowAbstention: boolean
  tieHandling: TieHandling
  nominationRequiresApproval: boolean
}

export interface CreatePositionInput {
  title: string
  description: string
  displayOrder: number
  seatCount: number
  maxSelections: number
}

export interface UpdatePositionInput extends CreatePositionInput {
  isActive: boolean
}

export interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
  errors?: Record<string, string[]>
}

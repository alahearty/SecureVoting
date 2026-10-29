import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import type { Role } from '@/api/types'
import AppShell from '@/components/AppShell.vue'
import { useAuthStore } from '@/stores/auth'

declare module 'vue-router' {
  interface RouteMeta {
    title?: string
    requiresAuth?: boolean
    guestOnly?: boolean
    roles?: Role[]
  }
}

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'login',
    component: () => import('@/views/auth/LoginView.vue'),
    meta: { title: 'Sign in', guestOnly: true }
  },
  {
    path: '/activate',
    name: 'activate',
    component: () => import('@/views/auth/ActivateView.vue'),
    meta: { title: 'Activate account', guestOnly: true }
  },
  {
    path: '/forgot-password',
    name: 'forgot-password',
    component: () => import('@/views/auth/ForgotPasswordView.vue'),
    meta: { title: 'Password recovery', guestOnly: true }
  },
  {
    path: '/reset-password',
    name: 'reset-password',
    component: () => import('@/views/auth/ResetPasswordView.vue'),
    meta: { title: 'Reset password', guestOnly: true }
  },
  {
    path: '/',
    component: AppShell,
    meta: { requiresAuth: true },
    children: [
      {
        path: '',
        name: 'home',
        redirect: () => useAuthStore().homePath
      },
      {
        path: 'student',
        name: 'student-dashboard',
        component: () => import('@/views/student/StudentDashboardView.vue'),
        meta: { title: 'Student home', roles: ['Student'] }
      },
      {
        path: 'student/elections',
        name: 'student-elections',
        component: () => import('@/views/student/StudentElectionsView.vue'),
        meta: { title: 'Elections', roles: ['Student'] }
      },
      {
        path: 'student/elections/:id',
        name: 'student-election',
        component: () => import('@/views/student/StudentElectionView.vue'),
        meta: { title: 'Election details', roles: ['Student'] }
      },
      {
        path: 'student/elections/:id/ballot',
        name: 'ballot',
        component: () => import('@/views/student/BallotView.vue'),
        meta: { title: 'Cast ballot', roles: ['Student'] }
      },
      {
        path: 'student/elections/:id/confirmed',
        name: 'ballot-confirmed',
        component: () => import('@/views/student/BallotConfirmationView.vue'),
        meta: { title: 'Ballot recorded', roles: ['Student'] }
      },
      {
        path: 'student/results',
        name: 'student-results',
        component: () => import('@/views/student/StudentResultsView.vue'),
        meta: { title: 'Published results', roles: ['Student', 'Candidate'] }
      },
      {
        path: 'candidate',
        name: 'candidate-dashboard',
        component: () => import('@/views/candidate/CandidateDashboardView.vue'),
        meta: { title: 'Candidate portal', roles: ['Student', 'Candidate'] }
      },
      {
        path: 'admin',
        name: 'admin-dashboard',
        component: () => import('@/views/admin/AdminDashboardView.vue'),
        meta: {
          title: 'Administration',
          roles: ['SuperAdministrator', 'ElectoralOfficer']
        }
      },
      {
        path: 'admin/elections',
        name: 'admin-elections',
        component: () => import('@/views/admin/AdminElectionsView.vue'),
        meta: { title: 'Election administration', roles: ['ElectoralOfficer', 'SuperAdministrator'] }
      },
      {
        path: 'admin/elections/new',
        name: 'admin-election-create',
        component: () => import('@/views/admin/CreateElectionView.vue'),
        meta: { title: 'Create election', roles: ['ElectoralOfficer', 'SuperAdministrator'] }
      },
      {
        path: 'admin/elections/:id',
        name: 'admin-election',
        component: () => import('@/views/admin/ManageElectionView.vue'),
        meta: { title: 'Manage election', roles: ['ElectoralOfficer', 'SuperAdministrator'] }
      },
      {
        path: 'admin/results',
        name: 'admin-results',
        component: () => import('@/views/results/ResultReviewView.vue'),
        meta: {
          title: 'Result review',
          roles: ['ElectoralOfficer', 'SuperAdministrator']
        }
      },
      {
        path: 'admin/results/:id',
        name: 'admin-result-detail',
        component: () => import('@/views/results/ResultDetailView.vue'),
        meta: {
          title: 'Result review',
          roles: ['ElectoralOfficer', 'SuperAdministrator']
        }
      },
      {
        path: 'admin/users',
        name: 'admin-users',
        component: () => import('@/views/admin/UsersView.vue'),
        meta: { title: 'User accounts', roles: ['SuperAdministrator'] }
      },
      {
        path: 'admin/settings',
        name: 'admin-settings',
        component: () => import('@/views/admin/SettingsView.vue'),
        meta: { title: 'System settings', roles: ['SuperAdministrator'] }
      },
      {
        path: 'admin/audit',
        name: 'admin-audit',
        component: () => import('@/views/audit/AuditView.vue'),
        meta: { title: 'Audit trail', roles: ['SuperAdministrator'] }
      },
      {
        path: 'observer',
        name: 'observer-dashboard',
        component: () => import('@/views/observer/ObserverDashboardView.vue'),
        meta: { title: 'Observer overview', roles: ['Observer'] }
      },
      {
        path: 'observer/elections',
        name: 'observer-elections',
        component: () => import('@/views/observer/ObserveElectionsView.vue'),
        meta: { title: 'Election review', roles: ['Observer'] }
      },
      {
        path: 'observer/results/:id',
        name: 'observer-result',
        component: () => import('@/views/results/ResultDetailView.vue'),
        meta: { title: 'Result review', roles: ['Observer'] }
      },
      {
        path: 'observer/audit',
        name: 'observer-audit',
        component: () => import('@/views/audit/AuditView.vue'),
        meta: { title: 'Audit trail', roles: ['Observer'] }
      },
      {
        path: 'results/:id',
        name: 'published-result',
        component: () => import('@/views/results/PublishedResultView.vue'),
        meta: { title: 'Published election result' }
      },
      {
        path: 'access-denied',
        name: 'access-denied',
        component: () => import('@/views/shared/AccessDeniedView.vue'),
        meta: { title: 'Access denied' }
      }
    ]
  },
  {
    path: '/:pathMatch(.*)*',
    name: 'not-found',
    component: () => import('@/views/shared/NotFoundView.vue'),
    meta: { title: 'Page not found' }
  }
]

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
  scrollBehavior: () => ({ top: 0 })
})

router.beforeEach(async (to) => {
  const auth = useAuthStore()
  await auth.bootstrap()

  if (to.meta.guestOnly && auth.isAuthenticated) {
    return auth.homePath
  }

  if (to.meta.requiresAuth && !auth.isAuthenticated) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }

  if (to.meta.roles && !to.meta.roles.some((role) => auth.hasRole(role))) {
    return auth.isAuthenticated ? auth.homePath : { name: 'login' }
  }

  return true
})

router.afterEach((to) => {
  const suffix = 'IAUE SecureVote'
  document.title = to.meta.title ? `${to.meta.title} · ${suffix}` : suffix
})

export default router

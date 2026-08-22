import { createRouter, createWebHistory } from 'vue-router'
import HomeView from '../components/Home.vue'
import Login from '../components/Login.vue'
import { msalInstance } from '../authConfig'
import AppLayout from '../layouts/AppLayout.vue'
import AuthLayout from '../layouts/AuthLayout.vue'
import VehiclesReport from '../components/VehiclesReport.vue'
import OperatorsReport from '../components/OperatorsReport.vue'
import Empty from '../components/Empty.vue'
import Projects from '../components/Projects.vue'

const routes = [
  {
    path: '/',
    component: AppLayout,
    meta: { requiresAuth: true },
    children: [
      {
        //Pusta ścieżka oznacza główny adres pod "/"
        path: '', 
        name: 'home',
        component: HomeView
      },
      {
        path: '/projects',
        name: 'projects',
        component: Projects,
      },
      {
        path: '/reports/vehicles',
        name: 'reports_vehicles',
        component: VehiclesReport,
      },
      {
        path: '/reports/operators',
        name: 'reports_operators',
        component: OperatorsReport,
      },
      {
        //do testów
        path: '/empty',
        name: 'empty',
        component: Empty,
      }
    ]
  },
  {
    path: '/login',
    component: AuthLayout,
    meta: { requiresAuth: false },
    children: [
      {
        path: '',
        name: 'login',
        component: Login
      }
    ]
  }
]

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes
})

router.beforeEach((to, from, next) => {
  const isAuthenticated = msalInstance.getAllAccounts().length > 0;

  //Sprawdzenie, czy jakakolwiek ze ścieżek nadrzędnych/docelowych wymaga autoryzacji
  const requiresAuth = to.matched.some(record => record.meta.requiresAuth);

  if (requiresAuth && !isAuthenticated) {
    next({ name: 'login' });
  } else if (to.name === 'login' && isAuthenticated) {
    //Jeśli zalogowany użytkownik próbuje wejść na stronę logowania - redirect na home
    next({ name: 'home' });
  } else {
    next();
  }
});

export default router

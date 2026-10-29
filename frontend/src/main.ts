import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import './style.css'
import { useAuthStore } from './stores/auth'

const app = createApp(App)
const pinia = createPinia()
app.use(pinia)
app.use(router)

const auth = useAuthStore(pinia)
window.addEventListener('securevote:unauthorized', () => {
  auth.clearSession()
})

void auth.bootstrap()

app.mount('#app')

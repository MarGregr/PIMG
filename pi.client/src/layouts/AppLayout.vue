<template>
  <SettingsModal ref="modalRef" />
  <Toolbar class="app-toolbar">
    <template #start>
      <div class="toolbar-start">
        <span class="app-logo">
          <i class="pi pi-bolt logo-icon" />
        </span>

        <Menubar :model="menuItems" class="toolbar-menubar"
                 :pt="{ submenu: { class: 'toolbar-submenu' } }">
          <template #item="{ item, props, hasSubmenu }">

            <router-link v-if="item.route" v-slot="{ href, navigate }" :to="item.route" custom>
              <a :href="href" v-bind="props.action" @click="navigate">
                <span v-if="item.icon" :class="item.icon" class="p-menuitem-icon" />
                <span class="p-menuitem-text">{{ item.label }}</span>
              </a>
            </router-link>

            <a v-else :href="item.url" :target="item.target" v-bind="props.action">
              <span v-if="item.icon" :class="item.icon" class="p-menuitem-icon" />
              <span class="p-menuitem-text">{{ item.label }}</span>
              <span v-if="hasSubmenu" class="pi pi-angle-down p-submenu-icon" style="margin-left: auto;" />
            </a>

          </template>
        </Menubar>
      </div>
    </template>

    <template #end>
      <div class="toolbar-end">
        <Divider layout="vertical" />

        <div class="user-section" @click="toggleUserMenu" ref="userTrigger">
          <Avatar :label="monogram" shape="circle" class="user-avatar" />
          <div class="user-info">
            <span class="user-name">{{ account.name }}</span>
          </div>
          <i class="pi pi-chevron-down chevron-icon" />
        </div>

        <Menu ref="userMenu" :model="userMenuItems" popup />
      </div>
    </template>
  </Toolbar>

  <RouterView />
</template>

<script setup>
  import { ref, computed } from 'vue'
  import Toolbar from 'primevue/toolbar'
  import Menubar from 'primevue/menubar'
  import Avatar from 'primevue/avatar'
  import Menu from 'primevue/menu'
  import Divider from 'primevue/divider'
  import { msalInstance } from './../authConfig';
  import SettingsModal from '../components/Settings/SettingsModal.vue'

  const account = ref(msalInstance.getAllAccounts()[0] ?? null);

  const monogram = computed(() => {
    if (!account.value?.name) return ''
    return account.value.name
      .split(' ')
      .filter(w => w.length > 0)
      .map(w => w[0].toUpperCase())
      .join('')
  })

  const modalRef = ref(null)
  const userMenu = ref()
  const toggleUserMenu = (event) => userMenu.value.toggle(event)

  const menuItems = ref([
    { label: 'Mapa', icon: 'pi pi-map', route: '/' },
    {
      label: 'Projekty',
      icon: 'pi pi-folder',
      items: [
        { label: 'Lista', icon: 'pi pi-folder-open', route: '/projects' },
      ]
    },
    {
      label: 'Raporty',
      icon: 'pi pi-chart-bar',
      items: [
        { label: 'Rejestracje pojazdów', icon: 'pi pi-car', route: '/reports/vehicles' },
        { label: 'Operatorzy', icon: 'pi pi-sitemap', route: '/reports/operators' },
      ]
    }
  ])

  const userMenuItems = ref([
    {
      label: 'Ustawienia',
      icon: 'pi pi-cog',
      command: async () => {
        try {
          modalRef.value?.open();

        } catch (error) {
          console.error("Błąd podczas wylogowywania:", error)
        }
      }
    },
    {
      label: 'Wyloguj się',
      icon: 'pi pi-sign-out',
      command: async () => {
        try {
          const accounts = msalInstance.getAllAccounts();
          await msalInstance.logoutRedirect({
            account: accounts[0] || null,
            postLogoutRedirectUri: window.location.origin + '/login'
          });
        } catch (error) {
          console.error("Błąd podczas wylogowywania:", error)
        }
      }
    }
  ])
</script>

<style scoped>
  .app-toolbar {
    padding: 0 1.5rem;
    height: 60px;
    border-radius: 0;
    border-left: none;
    border-right: none;
    border-top: none;
    background: #ffffff;
    box-shadow: 0 1px 3px rgba(0, 0, 0, 0.08);
    position: relative;
    z-index: 10;
    transition: opacity 0.2s;
  }

  .toolbar-start {
    display: flex;
    align-items: center;
    gap: 2rem;
  }

  .app-logo {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    cursor: pointer;
    user-select: none;
  }

  .logo-icon {
    font-size: 1.4rem;
    color: #6366f1;
  }

  .toolbar-menubar :deep(.p-menubar) {
    background: transparent;
    border: none;
    padding: 0;
  }

  .toolbar-menubar :deep(.p-menubar-root-list) {
    gap: 0.25rem;
  }

  .toolbar-menubar :deep(.p-menuitem-link) {
    border-radius: 6px;
    padding: 0.45rem 0.75rem;
    font-size: 0.875rem;
    font-weight: 500;
    color: #374151;
    transition: background 0.15s, color 0.15s;
  }

  .toolbar-menubar :deep(.p-menuitem-link:hover) {
    background: #f3f4f6;
    color: #6366f1;
  }

  .toolbar-menubar :deep(.p-menuitem-link .p-menuitem-icon) {
    color: #9ca3af;
    font-size: 0.8rem;
  }

  .toolbar-end {
    display: flex;
    align-items: center;
    gap: 0.75rem;
  }

  .user-section {
    display: flex;
    align-items: center;
    gap: 0.6rem;
    cursor: pointer;
    padding: 0.35rem 0.75rem 0.35rem 0.35rem;
    border-radius: 9999px;
    transition: background 0.15s;
    user-select: none;
  }

    .user-section:hover {
      background: #f3f4f6;
    }

  .user-avatar {
    background: linear-gradient(135deg, #6366f1, #8b5cf6);
    color: #ffffff;
    font-size: 0.75rem;
    font-weight: 700;
    width: 34px;
    height: 34px;
    flex-shrink: 0;
  }

  .user-info {
    display: flex;
    flex-direction: column;
    line-height: 1.2;
  }

  .user-name {
    font-size: 0.825rem;
    font-weight: 600;
    color: #111827;
  }

  .chevron-icon {
    font-size: 0.65rem;
    color: #9ca3af;
    margin-left: 0.1rem;
  }

  :deep(.p-divider.p-divider-vertical) {
    height: 24px;
    margin: 0 0.25rem;
    border-color: #e5e7eb;
  }
</style>

<style>
  /* Przyciemnienie toolbara */
  body.p-overflow-hidden .app-toolbar {
    opacity: 0.45;
    pointer-events: none;
  }

  /* Submenu */
  .toolbar-submenu {
    z-index: 50 !important;
  }
</style>

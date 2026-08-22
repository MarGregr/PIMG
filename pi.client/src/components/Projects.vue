<template>
  <div class="projects-panel">
    <ProjectsList :projects="projects"
                  :loading="loadingList"
                  @create="openCreate"
                  @edit="openEdit" />

    <ProjectFormModal v-if="modalVisible"
                      ref="modalRef"
                      :editing-project="editingProject"
                      @saved="onSaved"
                      @close="modalVisible = false" />
  </div>
</template>

<script setup>
  import { ref, nextTick, onMounted } from 'vue';
  import ProjectsList from './ProjectsList.vue';
  import ProjectFormModal from './ProjectFormModal.vue';
  import apiClient from '../services/api';

  /**
   * @typedef {Object} Project
   * @property {string} id
   * @property {string} name
   * @property {string} description
   * @property {number} lat
   * @property {number} lng
   * @property {number} radius
   * @property {string} createdAt
   * @property {string} updatedAt
   */

  const emit = defineEmits(['project-created', 'project-updated']);

  const projects = ref([]);
  const loadingList = ref(false);
  const modalVisible = ref(false);
  const editingProject = ref(null);
  const modalRef = ref(null);

  //Pobieranie listy 
  const fetchProjects = async () => {
    loadingList.value = true;
    try {
      //TODO: zastąpić wywołaniem Azure Function
      const res = await apiClient.get('/projects');
      projects.value = res.data;
    } finally {
      loadingList.value = false;
    }
  };

  //Otwieranie modalu
  const openCreate = async () => {
    editingProject.value = null;
    modalVisible.value = true;
    await nextTick();
    modalRef.value?.open();
  };

  const openEdit = async (project) => {
    editingProject.value = project;
    modalVisible.value = true;
    await nextTick();
    modalRef.value?.open();
  };

  //Obsługa zapisu
  const onSaved = (/** @type {Project} */ payload) => {
    const idx = projects.value.findIndex(p => p.id === payload.id);
    if (idx !== -1) {
      projects.value[idx] = payload;
      emit('project-updated', payload);
    } else {
      projects.value.push(payload);
      emit('project-created', payload);
    }
  };

  onMounted(fetchProjects);
</script>

<style scoped>
  .projects-panel {
    display: flex;
    flex-direction: column;
  }
</style>

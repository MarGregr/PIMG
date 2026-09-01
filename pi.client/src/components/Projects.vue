<template>
  <ProjectsList :projects="projects"
                :loading="loadingList"
                @create="openCreate"
                @edit="openEdit"
                @delete="onDelete" />
</template>

<script setup>
  import { ref, nextTick, onMounted } from 'vue';
  import { useRouter } from 'vue-router'
  import ProjectsList from './Projects/ProjectsList.vue';
  import apiClient from '../services/api';

  const router = useRouter()
  const projects = ref([]);
  const loadingList = ref(false);

  //Pobieranie listy
  const fetchProjects = async () => {
    loadingList.value = true;
    try {
      const res = await apiClient.get('/projects');
      projects.value = res.data;
    } finally {
      loadingList.value = false;
    }
  };

  const openCreate = async () => {
    router.push("/projects/new");
  };

  const openEdit = async (project) => {
    console.log(project);
    router.push(`projects/${project.id}/edit`);
  };

  const onDelete = async (project) => {
    console.log(project);
    try {

      const url = `/projects/${project.id}`;

      const response = await apiClient.delete(url);
      const index = projects.value.findIndex(p => p.id === project.id);
      projects.value.splice(index, 1);
    } catch (err) {
      console.error('Błąd podczas usuwania projektu:', err);
    } finally {
    }
  };

  onMounted(fetchProjects);
</script>


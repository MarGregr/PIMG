<template>
  <div class="projects-list">
    <DataTable :value="projects"
               :loading="loading"
               dataKey="id"
               size="small"
               stripedRows
               emptyMessage="Brak projektów. Dodaj pierwszy projekt poniżej.">
      <template #header>
        <div class="table-header">
          <span class="table-title">Projekty stacji ładowania</span>
          <span class="table-count" v-if="projects.length">{{ projects.length }} projektów</span>
        </div>
      </template>

      <Column field="name" header="Nazwa" style="min-width: 180px">
        <template #body="{ data }">
          <span class="project-name">{{ data.name }}</span>
        </template>
      </Column>

      <Column field="description" header="Opis" style="min-width: 200px">
        <template #body="{ data }">
          <span class="project-desc">{{ data.description || '—' }}</span>
        </template>
      </Column>

      <Column header="Lokalizacja" style="min-width: 170px">
        <template #body="{ data }">
          <span class="coords">
            <i class="pi pi-map-marker coords-icon" />
            {{ data.lat.toFixed(4) }}, {{ data.lng.toFixed(4) }}
          </span>
        </template>
      </Column>

      <Column field="radius" header="Zasięg" style="width: 100px">
        <template #body="{ data }">
          <Tag :value="formatRadius(data.radius)" severity="info" />
        </template>
      </Column>

      <Column field="createdAt" header="Utworzono" style="width: 130px">
        <template #body="{ data }">
          <span class="date-cell">{{ formatDate(data.createdAt) }}</span>
        </template>
      </Column>

      <Column style="width: 80px">
        <template #body="{ data }">
          <Button icon="pi pi-pencil"
                  text
                  rounded
                  size="small"
                  severity="secondary"
                  @click="emit('edit', data)"
                  v-tooltip.left="'Edytuj projekt'" />
        </template>
      </Column>
    </DataTable>

    <div class="add-row">
      <Button label="Nowy projekt"
              icon="pi pi-plus"
              outlined
              @click="emit('create')" />
    </div>
  </div>
</template>

<script setup>
  import DataTable from 'primevue/datatable';
  import Column from 'primevue/column';
  import Button from 'primevue/button';
  import Tag from 'primevue/tag';

  defineProps({
    /** @type {import('./ProjectFormModal.vue').Project[]} */
    projects: { type: Array, default: () => [] },
    loading: { type: Boolean, default: false },
  });

  const emit = defineEmits(['create', 'edit']);

  const formatRadius = (r) =>
    r >= 1000 ? `${(r / 1000).toFixed(1).replace('.0', '')} km` : `${r} m`;

  const formatDate = (iso) => {
    if (!iso) return '—';
    return new Date(iso).toLocaleDateString('pl-PL', {
      day: '2-digit', month: '2-digit', year: 'numeric',
    });
  };
</script>

<style scoped>
  .projects-list {
    display: flex;
    flex-direction: column;
  }

  .table-header {
    display: flex;
    align-items: center;
    gap: 0.75rem;
  }

  .table-title {
    font-size: 0.9375rem;
    font-weight: 600;
    color: #111827;
  }

  .table-count {
    font-size: 0.75rem;
    color: #6b7280;
    background: #f3f4f6;
    border-radius: 99px;
    padding: 2px 10px;
  }

  .project-name {
    font-weight: 500;
    color: #111827;
  }

  .project-desc {
    font-size: 0.8125rem;
    color: #6b7280;
  }

  .coords {
    font-size: 0.8125rem;
    color: #374151;
    display: flex;
    align-items: center;
    gap: 4px;
    font-variant-numeric: tabular-nums;
  }

  .coords-icon {
    color: #2563eb;
    font-size: 0.75rem;
  }

  .date-cell {
    font-size: 0.8125rem;
    color: #6b7280;
  }

  .add-row {
    padding: 1rem 0 0.25rem;
    display: flex;
    justify-content: flex-start;
  }
</style>

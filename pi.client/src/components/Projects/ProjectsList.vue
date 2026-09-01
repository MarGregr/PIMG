<template>
  <ConfirmDialog />
  <div>
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

      <Column field="createdAt" header="Utworzono" style="width: 130px">
        <template #body="{ data }">
          <span class="date-cell">{{ formatDate(data.createdAt) }}</span>
        </template>
      </Column>

      <Column style="width: 80px">
        <template #body="{ data, index }">
          <!--Edycja-->
          <Button icon="pi pi-pencil"
                  text
                  rounded
                  size="small"
                  severity="secondary"
                  @click="emit('edit', data)"
                  v-tooltip.left="'Edytuj projekt'" />
          <!--Usuwanie-->
          <Button icon="pi pi-trash"
                  text
                  rounded
                  size="small"
                  severity="danger"
                  @click="confirmDelete(index, data)"
                  v-tooltip.left="'Usuń'" />
        </template>
      </Column>
    </DataTable>

    <div>
      <Button label="Nowy projekt"
              icon="pi pi-plus" class="mt-3"
              @click="emit('create')" />
    </div>

  </div>
</template>

<script setup>
  import { ref } from 'vue';
  import DataTable from 'primevue/datatable';
  import Column from 'primevue/column';
  import Button from 'primevue/button';
  import Tag from 'primevue/tag';
  import ConfirmDialog from 'primevue/confirmdialog';
  import { useConfirm } from 'primevue/useconfirm';

  const props = defineProps({
    projects: { type: Array, default: () => [] },
    loading: { type: Boolean, default: false },
  });

  // const projects = ref([...props.projects]);

  const confirm = useConfirm();
  const emit = defineEmits(['create', 'edit', 'delete']);

  const formatDate = (iso) => {
    if (!iso) return '—';
    return new Date(iso).toLocaleDateString('pl-PL', {
      day: '2-digit', month: '2-digit', year: 'numeric',
    });
  };

  const confirmDelete = (index, data) => {
    console.log(data)
    confirm.require({
      message: 'Czy na pewno chcesz usunąć ten projekt?',
      header: 'Potwierdzenie usunięcia',
      icon: 'pi pi-exclamation-triangle',
      rejectProps: {
        label: 'Anuluj',
        severity: 'secondary',
        outlined: true
      },
      acceptProps: {
        label: 'Usuń',
        severity: 'danger'
      },
      accept: () => {
        // projects.value.splice(index, 1);
        emit('delete', data);
      }
    });
  };

</script>

<style scoped>
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
</style>

<template>
  <div class="card">
    <h2>Operatorzy</h2>

    <div v-if="loading" class="flex justify-content-center padding-2">
      <ProgressSpinner />
    </div>

    <div v-else-if="error" class="p-error">
      {{ error }}
    </div>

    <div v-else>
      <DataTable :value="tableData"
                 stripedRows
                 responsiveLayout="scroll"
                 class="p-datatable-sm mt-4">

        <Column field="name" header="Operator" sortable class="font-bold"></Column>
        <Column field="poolsQuantity" header="Stacje" sortable class="font-bold" bodyClass="text-right" headerClass="text-right-header"></Column>
      </DataTable>
    </div>
  </div>
</template>

<script setup>
  import { ref, onMounted, computed } from 'vue';
  import ProgressSpinner from 'primevue/progressspinner';
  import DataTable from 'primevue/datatable';
  import Column from 'primevue/column';
  import apiClient from '../services/api';

  const loading = ref(true);
  const error = ref(null);

  const tableData = ref([])

  const fetchData = async () => {
    try {
      loading.value = true;
      error.value = null;

      const response = await apiClient.get('/reports/operators');
      tableData.value = await response.data;
    } catch (err) {
      error.value = err.message || 'Wystąpił nieoczekiwany błąd.';
      console.error(err);
    } finally {
      loading.value = false;
    }
  };

  onMounted(() => {
    fetchData();
  });
</script>

<style scoped>
  .card {
    background: var(--surface-card);
    padding: 2rem;
    border-radius: 10px;
    margin-bottom: 2rem;
    box-shadow: 0 2px 1px -1px rgba(0,0,0,.2), 0 1px 1px 0 rgba(0,0,0,.14), 0 1px 3px 0 rgba(0,0,0,.12);
  }

  .h-30rem {
    height: 30rem;
  }

  .p-error {
    color: #e24c4c;
    font-weight: bold;
  }

  .my-4 {
    margin-top: 1.5rem;
    margin-bottom: 1.5rem;
  }

  .mt-4 {
    margin-top: 1.5rem;
  }

  /* Kolor dla globalnego motywu PrimeVue w kolumnie Suma */
  :deep(.text-primary) {
    font-weight: 600;
/*    color: var(--primary-color, #42A5F5) !important;*/
  }

  /* Wyrównanie do prawej w datatable */

  /* Wyrównanie samych liczb w komórkach */
  :deep(.text-right) {
    text-align: right !important;
  }

  /* Wyrównanie tekstu, nagłówka i strzałki sortowania (Flexbox) */
  :deep(.text-right-header) {
    text-align: right !important;
    justify-content: flex-end !important;
  }
</style>

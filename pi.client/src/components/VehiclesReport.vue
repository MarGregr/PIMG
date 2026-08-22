<template>
  <div class="card">
    <h2>Rejestracje pojazdów BEV (według daty ostatniej rejestracji)</h2>

    <div v-if="loading" class="flex justify-content-center padding-2">
      <ProgressSpinner />
    </div>

    <div v-else-if="error" class="p-error">
      {{ error }}
    </div>

    <div v-else>
      <Chart type="bar"
             :data="chartData"
             :options="chartOptions"
             class="h-30rem" />

      <div class="my-4"></div>

      <DataTable :value="tableData"
                 stripedRows
                 responsiveLayout="scroll"
                 class="p-datatable-sm mt-4">

        <Column field="year" header="Rok" sortable class="font-bold"></Column>

        <Column v-for="col in tableColumns"
                :key="col"
                :field="col"
                :header="col"
                sortable
                headerClass="text-right-header"
                bodyClass="text-right">
          <template #body="slotProps">
            {{ formatNumber(slotProps.data[col]) }}
          </template>
        </Column>

        <Column field="total"
                header="Suma Razem"
                sortable
                class="font-bold text-primary"
                headerClass="text-right-header"
                bodyClass="text-right">
          <template #body="slotProps">
            {{ formatNumber(slotProps.data.total) }}
          </template>
        </Column>
      </DataTable>
    </div>
  </div>
</template>

<script setup>
  import { ref, onMounted, computed } from 'vue';
  import Chart from 'primevue/chart';
  import ProgressSpinner from 'primevue/progressspinner';
  import DataTable from 'primevue/datatable';
  import Column from 'primevue/column';
  import apiClient from '../services/api';

  const chartData = ref({ labels: [], datasets: [] });
  const chartOptions = ref({});
  const loading = ref(true);
  const error = ref(null);

  const tableColumns = computed(() => {
    return chartData.value.datasets.map(dataset => dataset.label);
  });

  const tableData = computed(() => {
    const labels = chartData.value.labels || [];
    const datasets = chartData.value.datasets || [];

    return labels.map((year, yearIndex) => {
      const row = { year: year, total: 0 };

      datasets.forEach(dataset => {
        const value = dataset.data[yearIndex] || 0;
        row[dataset.label] = value;
        row.total += value;
      });

      return row;
    });
  });

  //Formatowanie liczb do polskiego standardu (np. 12345 -> 12 345)
  const formatNumber = (value) => {
    if (value === undefined || value === null) return 0;
    return value.toLocaleString('pl-PL');
  };

  const transformData = (rawData) => {
    const years = [...new Set(rawData.map(item => item.rok))].sort((a, b) => a - b);
    const vehicleTypes = [...new Set(rawData.map(item => item.rodzaj_pojazdu))];
    const colors = ['#42A5F5', '#66BB6A', '#FFA726', '#AB47BC', '#EC407A'];

    const datasets = vehicleTypes.map((type, index) => {
      const dataForYears = years.map(year => {
        const found = rawData.find(item => item.rok === year && item.rodzaj_pojazdu === type);
        return found ? found.liczba : 0;
      });

      return {
        label: type,
        data: dataForYears,
        backgroundColor: colors[index % colors.length],
        borderColor: colors[index % colors.length],
        borderWidth: 1,
        stack: 'v-stack'
      };
    });

    return {
      labels: years,
      datasets: datasets
    };
  };

  const fetchData = async () => {
    try {
      loading.value = true;
      error.value = null;

      const response = await apiClient.get('/reports/vehicles');
      const rawData = await response.data;
      chartData.value = transformData(rawData);

    } catch (err) {
      error.value = err.message || 'Wystąpił nieoczekiwany błąd.';
      console.error(err);
    } finally {
      loading.value = false;
    }
  };

  //Konfiguracja osi i styli wykresu
  const setChartOptions = () => {
    chartOptions.value = {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: {
          labels: { color: '#495057' }
        },
        tooltip: {
          mode: 'index',
          intersect: false
        }
      },
      scales: {
        x: {
          stacked: true,
          ticks: { color: '#495057' },
          grid: { color: '#ebedef' }
        },
        y: {
          stacked: true,
          beginAtZero: true,
          ticks: { color: '#495057' },
          grid: { color: '#ebedef' }
        }
      }
    };
  };

  onMounted(() => {
    setChartOptions();
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

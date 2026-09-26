<template>
  <div class="card">
    <h2 class="mb-4">Rejestracje pojazdów BEV (według daty ostatniej rejestracji)</h2>

    <div class="filter-container mb-4">
      <label for="voivodeship-select" class="filter-label">Województwo:</label>
      <Select id="voivodeship-select"
              v-model="selectedVoivodeship"
              :options="voivodeshipsOptions"
              optionLabel="label"
              optionValue="value"
              placeholder="Wybierz województwo"
              class="filter-select"
              :disabled="loadingVoivodeships"
              @change="fetchData" />
    </div>

    <div class="filter-container mb-4">
      <label for="fueltype-select" class="filter-label">BEV / hybrydy:</label>
      <Select id="fueltype-select"
              v-model="selectedFuelType"
              :options="fuelTypeOptions"
              optionLabel="label"
              optionValue="value"
              placeholder="Wybierz BEV / hybrydy"
              class="filter-select"
              :disabled="loadingVoivodeships"
              @change="fetchData" />
    </div>

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
  import Select from 'primevue/select';
  import apiClient from '../../services/api';

  const VEHICLE_TYPE_COLORS = {
    'CIĄGNIK ROLNICZY': '#8D6E63',    // Brązowy
    'MOTOCYKL': '#AB47BC',            // Fioletowy
    'MOTOROWER': '#EC407A',           // Różowy
    'SAMOCHÓD OSOBOWY': '#42A5F5',    // Niebieski
    'AUTOBUS': '#FFA726',             // Pomarańczowy
    'SAMOCHÓD CIĘŻAROWY': '#66BB6A',  // Zielony
    'TROLEJBUS': '#26A69A'            // Turkusowy
  };

  const FALLBACK_COLORS = ['#78909C', '#5C6BC0', '#D4E157', '#FF7043'];

  const getColorForType = (type, fallbackIndex) => {
    const normalizedType = type ? type.trim().toUpperCase() : '';
    if (VEHICLE_TYPE_COLORS[normalizedType]) {
      return VEHICLE_TYPE_COLORS[normalizedType];
    }
    return FALLBACK_COLORS[fallbackIndex % FALLBACK_COLORS.length];
  };

  const chartData = ref({ labels: [], datasets: [] });
  const chartOptions = ref({});
  const loading = ref(true);
  const error = ref(null);

  const selectedVoivodeship = ref("ALL");
  const voivodeshipsList = ref([]);
  const loadingVoivodeships = ref(false);

  const voivodeshipsOptions = computed(() => {
    return [
      { label: 'Wszystkie województwa', value: "ALL" },
      ...voivodeshipsList.value.map(item => ({
        label: item.name,
        value: item.name
      }))
    ];
  });

  const selectedFuelType = ref(1);

  const fuelTypeOptions = computed(() => {
    return [
      { label: 'tylko BEV', value: 1 },
      { label: 'tylko hybrydowe', value: 2 },
      { label: 'BEV oraz hybrydowe', value: 0 },
    ];
  });

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

  const formatNumber = (value) => {
    if (value === undefined || value === null) return 0;
    return value.toLocaleString('pl-PL');
  };

  const transformData = (rawData) => {
    const years = [...new Set(rawData.map(item => item.rok))].sort((a, b) => a - b);
    const vehicleTypes = [...new Set(rawData.map(item => item.rodzaj_pojazdu))];

    const datasets = vehicleTypes.map((type, index) => {
      const dataForYears = years.map(year => {
        const found = rawData.find(item => item.rok === year && item.rodzaj_pojazdu === type);
        return found ? found.liczba : 0;
      });

      const color = getColorForType(type, index);

      return {
        label: type,
        data: dataForYears,
        backgroundColor: color,
        borderColor: color,
        borderWidth: 1,
        stack: 'v-stack'
      };
    });

    return {
      labels: years,
      datasets: datasets
    };
  };

  const fetchVoivodeships = async () => {
    try {
      loadingVoivodeships.value = true;
      const response = await apiClient.get('/reports/vehicles/voivodeships');
      voivodeshipsList.value = response.data;
    } catch (err) {
      console.error('Błąd podczas pobierania listy województw:', err);
    } finally {
      loadingVoivodeships.value = false;
    }
  };

  const fetchData = async () => {
    try {
      loading.value = true;
      error.value = null;

      const endpoint = selectedVoivodeship.value !== "ALL"
        ? `/reports/vehicles/${selectedFuelType.value}/${encodeURIComponent(selectedVoivodeship.value)}`
        : `/reports/vehicles/${selectedFuelType.value}`;

      const response = await apiClient.get(endpoint);
      const rawData = await response.data;
      chartData.value = transformData(rawData);

    } catch (err) {
      error.value = err.message || 'Wystąpił nieoczekiwany błąd.';
      console.error(err);
    } finally {
      loading.value = false;
    }
  };

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
    fetchVoivodeships();
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

  .filter-container {
    display: flex;
    align-items: center;
    gap: 1rem;
  }

  .filter-label {
    font-weight: 600;
    white-space: nowrap;
    margin: 0;
    line-height: 1;
  }

  .filter-select {
    width: 100%;
    max-width: 18rem;
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

  :deep(.text-primary) {
    font-weight: 600;
  }

  :deep(.text-right) {
    text-align: right !important;
  }

  :deep(.text-right-header) {
    text-align: right !important;
    justify-content: flex-end !important;
  }
</style>

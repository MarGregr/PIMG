<template>
    <Chart type="bar"
           :data="chartData"
           :options="chartOptions"
           class="h-200" />

  <!--<pre>{{ JSON.stringify(stats, null, 2) }}</pre>-->

</template>

<script setup>
  import { computed } from 'vue';
  import Chart from 'primevue/chart';

  const props = defineProps({
    stats: {
      type: Array,
      required: true,
      default: () => []
    }
  });

  const chartData = computed(() => {
    return {
      labels: props.stats.map(item => item.Date),
      datasets: [
        {
          label: 'Liczba ładowań',
          data: props.stats.map(item => item.Count),
          backgroundColor: '#34d399',
          borderColor: '#10b981',
          borderWidth: 0,
          borderRadius: 6
        }
      ]
    };
  });

  const chartOptions = computed(() => {
    const documentStyle = getComputedStyle(document.documentElement);
    const textColor = documentStyle.getPropertyValue('--text-color') || '#495057';
    const surfaceBorder = documentStyle.getPropertyValue('--surface-border') || '#dee2e6';

    return {
      plugins: {
        legend: {
          display: false,
          labels: {
            color: textColor
          }
        }
      },
      scales: {
        x: {
          ticks: {
            color: textColor
          },
          grid: {
            color: surfaceBorder,
            drawBorder: false
          }
        },
        y: {
          beginAtZero: true, //Oś Y zawsze zaczyna się od 0
          ticks: {
            color: textColor,
            stepSize: 1 //Tylko pełne liczby
          },
          grid: {
            color: surfaceBorder,
            drawBorder: false
          }
        }
      },
      maintainAspectRatio: false,
      aspectRatio: 0.8
    };
  });
</script>

<style scoped>
  .card {
    background: var(--surface-card);
    padding: 2rem;
    border-radius: 10px;
    margin-bottom: 2rem;
  }

  .h-200 {
    height: 200px;
  }
</style>

<template>
  <div>
    <ConfirmDialog />

    <DataTable :value="chargingPoints"
               dataKey="id"
               size="small"
               stripedRows
               emptyMessage="Brak punktów ładowania">
      <template #header>
        <div class="table-header">
          <span class="table-title">Punkty ładowania</span>
        </div>
      </template>

      <Column field="power" header="Moc" style="min-width: 180px">
        <template #body="{ data }">
          <span>{{ data.power }} kW</span>
        </template>
      </Column>

      <Column field="price" header="Cena / kWh" style="min-width: 200px">
        <template #body="{ data }">
          <span>
            {{ data.price.toLocaleString('pl-PL', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }} zł
          </span>
        </template>
      </Column>

      <Column style="width: 100px">
        <template #body="{ data, index }">
          <Button icon="pi pi-pencil"
                  text
                  rounded
                  size="small"
                  severity="secondary"
                  @click="editChargingPoint(data, index)"
                  v-tooltip.left="'Edytuj'" />
          <Button icon="pi pi-trash"
                  text
                  rounded
                  size="small"
                  severity="danger"
                  @click="confirmDelete(index)"
                  v-tooltip.left="'Usuń'" />
        </template>
      </Column>
    </DataTable>

    <div>
      <Button label="Dodaj punkt ładowania"
              icon="pi pi-plus"
              class="mt-3"
              size="small"
              severity="primary"
              outlined
              @click="addChargingPoint" />
    </div>

    <ChargingPointModal ref="modalRef" @saved="onSaved" />
  </div>
</template>

<script setup>
  import { ref, watch } from 'vue';
  import DataTable from 'primevue/datatable';
  import Column from 'primevue/column';
  import Button from 'primevue/button';
  import ConfirmDialog from 'primevue/confirmdialog';
  import { useConfirm } from 'primevue/useconfirm';
  import ChargingPointModal from './ChargingPointModal.vue';

  const props = defineProps({
    chargingPoints: { type: Array, default: () => [] },
  });

  const emit = defineEmits(['changed']);

  const confirm = useConfirm();
  const modalRef = ref(null);

  const chargingPoints = ref([...props.chargingPoints]);

  watch(() => props.chargingPoints, (newVal) => {
    chargingPoints.value = [...newVal];
  }, { deep: true });

  const selectedIndex = ref(null);

  const addChargingPoint = () => {
    selectedIndex.value = null;
    modalRef.value?.open();
  };

  const editChargingPoint = (data, index) => {
    selectedIndex.value = index;
    modalRef.value?.open({ ...data });
  };

  const confirmDelete = (index) => {
    confirm.require({
      message: 'Czy na pewno chcesz usunąć ten punkt ładowania?',
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
        chargingPoints.value.splice(index, 1);
        emit('changed', chargingPoints.value);
      }
    });
  };

  const onSaved = (payload) => {
    if (selectedIndex.value !== null) {
      chargingPoints.value[selectedIndex.value] = payload;
    } else {
      chargingPoints.value.push(payload);
    }
    emit('changed', chargingPoints.value);
  };
</script>

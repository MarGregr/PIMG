<template>
  <Dialog v-model:visible="visible"
          @show="OnDialogShow"
          modal
          header="Punkt ładowania"
          :style="{ width: '50vw', maxWidth: '1000px' }"
          :draggable="false">

    <div class="field-block">
      <label class="field-label">Operator</label>
      <Select v-model="selectedOperator"
              :options="operators"
              optionLabel="name"
              filterBy="name"
              class="w-full"
              :filter="true"
              filterPlaceholder="Szukaj operatora..."
              checkmark />
      <Message size="small" severity="error" v-if="errors.operator">{{ errors.operator }}</Message>
    </div>

    <template #footer>
      <Button label="Anuluj" severity="secondary" @click="close" />
      <Button label="Zapisz"
              icon="pi pi-check"
              :disabled="isSaveDisabled"
              @click="submitForm" />
    </template>
  </Dialog>
</template>


<script setup>
  import { ref } from 'vue';
  import Dialog from 'primevue/dialog';
  import Button from 'primevue/button';
  import Select from 'primevue/select';
  import Message from 'primevue/message';
  import apiClient from './../../services/api';

  const visible = ref(false);
  const errors = ref({});
  const operators = ref([]);
  const selectedOperator = ref(null);
  const isSubmitting = ref(false);

  const open = () => {
    errors.value = {};
    visible.value = true;
  };

  const close = () => {
    visible.value = false;
  };

  const validate = () => {
    if (!selectedOperator.value) {
      errors.value = { operator: 'Operator jest wymagany' };
      return false;
    }
    errors.value = {};
    return true;
  };

  const submitForm = async () => {
    if (!validate()) return;

    isSubmitting.value = true;
    try {
      const payload = {
        operatorId: selectedOperator.value.id
      };

      await apiClient.put('/settings', payload);

      close();
    } catch (err) {
      console.error('Błąd podczas zapisywania operatora:', err);
    } finally {
      isSubmitting.value = false;
    }
  };

  const OnDialogShow = async () => {
    try {
      const opsResponse = await apiClient.get('/operators');
      operators.value = opsResponse.data;

      const settingsResponse = await apiClient.get('/settings');

      const currentOperatorId = settingsResponse.data?.operator_id;

      if (currentOperatorId) {
        selectedOperator.value = operators.value.find(op => op.id === currentOperatorId) || null;
      }
    } catch (err) {
      console.error('Błąd podczas pobierania danych operatorów:', err);
    }
  };

  defineExpose({ open });
</script>

<style scoped>
  .field {
    display: flex;
    flex-direction: column;
    gap: 0.45rem;
  }
</style>





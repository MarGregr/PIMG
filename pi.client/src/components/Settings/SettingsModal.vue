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
  import { ref, computed } from 'vue';
  import Dialog from 'primevue/dialog';
  import Button from 'primevue/button';
  import InputNumber from 'primevue/inputnumber';
  import Select from 'primevue/select';
  import Message from 'primevue/message';
  import apiClient from './../../services/api';

  const visible = ref(false);
  const errors = ref({});
  const operators = ref([]);

  const selectedOperator = ref(null);

  const open = () => {
    errors.value = {};
    visible.value = true;
  };

  const close = () => {
    visible.value = false;
  };

  const validate = () => {
    const e = {};
    if (selectedOperator.value === null) {
      e.operator = 'Operator jest wymagany';
      errors.value = e;
      return false;
    }
    errors.value = {};
    return true;
  };

  const submitForm = async () => {
    if (!validate()) return;

    try {

      const payload = {
        operatorId: selectedOperator.value.id,
      };

      const response = await apiClient.put('/settings', payload)
      close();
    } catch (err) {
      console.error('Błąd podczas ustawień operatora:', err);
    } finally {
    }
  };

  defineExpose({ open });

  const OnDialogShow = async () => {

    const response = await apiClient.get("/operators");
    operators.value = response.data;
  }
</script>

<style scoped>
  .field {
    display: flex;
    flex-direction: column;
    gap: 0.45rem;
  }
</style>





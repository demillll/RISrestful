document.querySelector('#imageForm').addEventListener('submit', async function (e) {
    e.preventDefault();
    let formData = new FormData(this);

    // Отображаем изображения в превью
    const file1 = document.querySelector('#image1').files[0];
    const file2 = document.querySelector('#image2').files[0];

    if (file1) {
        document.querySelector('#image1Preview').src = URL.createObjectURL(file1);
    } else {
        console.log("Первое изображение не выбрано");
    }

    if (file2) {
        document.querySelector('#image2Preview').src = URL.createObjectURL(file2);
    } else {
        console.log("Второе изображение не выбрано");
    }

    try {
        // Отправляем данные на сервер для сравнения
        let response = await fetch('/api/ImageComparison/compare', {
            method: 'POST',
            body: formData
        });

        if (!response.ok) {
            let error = await response.text();
            document.querySelector('#resultContainer').innerHTML =
                `<div class="alert alert-danger">Ошибка: ${error}</div>`;
            return;
        }

        let result = await response.json();
        document.querySelector('#resultContainer').innerHTML =
            `<div class="alert alert-info">Схожесть изображений: <strong>${result.similarity}%</strong></div>`;
    } catch (err) {
        document.querySelector('#resultContainer').innerHTML =
            `<div class="alert alert-danger">Ошибка: ${err.message}</div>`;
    }
});

// Добавление предпросмотра изображений
document.querySelector('#image1').addEventListener('change', function () {
    const file = this.files[0];
    if (file) {
        const img1Preview = document.querySelector('#image1Preview');
        img1Preview.src = URL.createObjectURL(file);
        img1Preview.style.display = 'block';
    }
});

document.querySelector('#image2').addEventListener('change', function () {
    const file = this.files[0];
    if (file) {
        const img2Preview = document.querySelector('#image2Preview');
        img2Preview.src = URL.createObjectURL(file);
        img2Preview.style.display = 'block';
    }
});


// Бенчмарк
document.querySelector('#benchmarkButton').addEventListener('click', async function () {
    let formData = new FormData(document.querySelector('#imageForm'));

    try {
        let response = await fetch('/api/ImageComparison/benchmark', {
            method: 'POST',
            body: formData
        });

        if (!response.ok) {
            let error = await response.text();
            document.querySelector('#benchmarkResult').innerHTML =
                `<div class="alert alert-danger">Ошибка: ${error}</div>`;
            return;
        }

        let result = await response.json();
        document.querySelector('#benchmarkResult').innerHTML = `
            <div class="alert alert-info">
                <p>Схожесть: <strong>${result.similarity.toFixed(2)}%</strong></p>
                <p>Время линейного метода: <strong>${result.linearTime} мс</strong></p>
                <p>Время многопоточного метода: <strong>${result.parallelTime} мс</strong></p>
            </div>`;
    } catch (err) {
        document.querySelector('#benchmarkResult').innerHTML =
            `<div class="alert alert-danger">Ошибка: ${err.message}</div>`;
    }
});

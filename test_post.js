async function test() {
    try {
        const res = await fetch('http://localhost:5001/api/Items', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                itemName: "Test",
                categoryId: 1,
                uomId: 1,
                minStockLevel: 10,
                maxStockLevel: 100,
                isActive: true
            })
        });
        const text = await res.text();
        console.log("SUCCESS:", res.status, text);
    } catch(e) {
        console.log("ERROR:", e.message);
    }
}
test();

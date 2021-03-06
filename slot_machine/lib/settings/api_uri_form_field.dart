import 'package:flutter/material.dart';

class ApiUriFormField extends StatelessWidget {
  const ApiUriFormField({
    Key? key,
    required this.controller,
  }) : super(key: key);

  final TextEditingController controller;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('API-url', style: theme.textTheme.subtitle1),
        TextFormField(
          controller: controller,
          validator: _validate,
          decoration: const InputDecoration(
            hintText: 'e.g.: https://google.com',
          ),
        ),
      ],
    );
  }

  String? _validate(String? value) {
    if (value == null || value.isEmpty) {
      return 'URL cannot be empty';
    }
    final uri = Uri.tryParse(value);
    if (uri == null) {
      return 'Please enter a valid url';
    }
    return null;
  }
}
